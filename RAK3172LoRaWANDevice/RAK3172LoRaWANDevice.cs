//---------------------------------------------------------------------------------
// Copyright (c) September 2021, devMobile Software
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
//---------------------------------------------------------------------------------
namespace devMobile.IoT.LoRaWAN.NetCore.RAK3172
{
   using System;
   using System.Diagnostics;
   using System.IO.Ports;
   using System.Text;
   using System.Threading;

   public enum LoRaClass
   {
      Undefined = 0,
      A,
      B,
      C
   }

   public enum LoRaConfirmType
   {
      Undefined = 0,
      Unconfirmed,
      Confirmed,
      Multicast,
      Proprietary
   }

   public enum Result
   {
      Undefined = 0,
      Success,
      ResponseInvalid,
      ATResponseTimeout,
      ATCommandUnsuported,
      ATCommandInvalidParameter,
      ErrorReadingOrWritingFlash,
      LoRaBusy,
      LoRaServiceIsUnknown,
      LoRaParameterInvalid,
      LoRaFrequencyInvalid,
      LoRaDataRateInvalid,
      LoRaFrequencyAndDataRateInvalid,
      LoRaDeviceNotJoinedNetwork,
      LoRaPacketToLong,
      LoRaServiceIsClosedByServer,
      LoRaRegionUnsupported,
      LoRaDutyCycleRestricted,
      LoRaNoValidChannelFound,
      LoRaNoFreeChannelFound,
      StatusIsError,
      LoRaTransmitTimeout,
      LoRaRX1Timeout,
      LoRaRX2Timeout,
      LoRaRX1ReceiveError,
      LoRaRX2ReceiveError,
      LoRaJoinFailed,
      LoRaDownlinkRepeated,
      LoRaPayloadSizeNotValidForDataRate,
      LoRaTooManyDownlinkFramesLost,
      LoRaAddressFail,
      LoRaMicVerifyError,
   }

   public sealed class Rak3172LoRaWanDevice : IDisposable
   {
      public const ushort BaudRateMinimum = 600;
      public const ushort BaudRateMaximum = 57600;
      public const ushort RegionIDLength = 5;
      public const ushort DevEuiLength = 16;
      public const ushort AppEuiLength = 16;
      public const ushort AppKeyLength = 32;
      public const ushort DevAddrLength = 8;
      public const ushort NwsKeyLength = 32;
      public const ushort AppsKeyLength = 32;
      public const ushort MessagePortMinimumValue = 1;
      public const ushort MessagePortMaximumValue = 223;
      public const ushort MessageBytesMinimumLength = 1;
      public const ushort MessageBytesMaximumLength = 242;
      public const ushort MessageBcdMinimumLength = 1;
      public const ushort MessageBcdMaximumLength = 484;

      public readonly TimeSpan SendTimeoutMinimum = new TimeSpan(0, 0, 1);
      public readonly TimeSpan SendTimeoutMaximum = new TimeSpan(0, 0, 10);

      public readonly TimeSpan JoinTimeoutMinimum = new TimeSpan(0, 0, 1);
      public readonly TimeSpan JoinTimeoutMaximum = new TimeSpan(0, 0, 20);

      private const string EndOfLineMarker = "\r\n";
      private const string JoinMarkerSuccess = "+EVT:JOINED";
      private const string JoinMarkerFailure = "+EVT:FAILED";

      private const string ConfirmedMarkerSuccess = "+EVT:SEND CONFIRMED OK";
      private const string ConfirmedMarkerFailure = "+EVT:SEND CONFIRMED FAILED";

      private const string ErrorMarker = "ERROR:";
      private const string ReplyMarker = "AT+RECV";
      private readonly TimeSpan CommandTimeoutDefault = new TimeSpan(0, 0, 3);

      private SerialPort serialDevice = null;

      private string atCommandExpectedResponse;
      private readonly AutoResetEvent atExpectedEvent;
      private StringBuilder response;
      private Result result;

      public delegate void JoinCompletionHandler(bool result);
      public JoinCompletionHandler onJoinCompletion;

      //public delegate void MessageConfirmationHandler(int rssi, int snr);
      public delegate void MessageConfirmationHandler(bool success);
      public MessageConfirmationHandler OnMessageConfirmation;
      public delegate void ReceiveMessageHandler(int port, int rssi, int snr, string payload);
      public ReceiveMessageHandler OnReceiveMessage;

      public Rak3172LoRaWanDevice()
      {
         response = new StringBuilder();
         this.atExpectedEvent = new AutoResetEvent(false);
      }

      public Result Initialise(string serialPortId, int baudRate, Parity serialParity, ushort dataBits, StopBits stopBits)
      {
         Result result;
         if ((serialPortId == null) || (serialPortId == ""))
         {
            throw new ArgumentException("Invalid SerialPortId", nameof(serialPortId));
         }
         if ((baudRate < BaudRateMinimum) || (baudRate > BaudRateMaximum))
         {
            throw new ArgumentException("Invalid BaudRate", nameof(baudRate));
         }

         serialDevice = new SerialPort(serialPortId);

         // set parameters
         serialDevice.BaudRate = baudRate;
         serialDevice.Parity = serialParity;
         serialDevice.DataBits = dataBits;
         serialDevice.StopBits = stopBits;
         serialDevice.Handshake = Handshake.None;

         serialDevice.NewLine = "\r\n";

         serialDevice.Open();

         atCommandExpectedResponse = string.Empty;

         serialDevice.DataReceived += SerialDevice_DataReceived;

         // Set the Working mode to LoRaWAN
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:work_mode LoRaWAN");
#endif
         result = SendCommand("OK", "AT+NWM=1", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:work_mode failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result Class(LoRaClass loRaClass)
      {
         string command;

         switch (loRaClass)
         {
            case LoRaClass.A:
               command = "AT+CLASS=A";
               break;
            case LoRaClass.B:
               command = "AT+CLASS=B";
               break;
            case LoRaClass.C:
               command = "AT+CLASS=C";
               break;
            default:
               throw new ArgumentException($"LoRa class value {loRaClass} invalid", nameof(loRaClass));
         }

         // Set the class
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+CLASS={loRaClass}");
#endif
         Result result = SendCommand("OK", command, CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+CLASS failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      /*
      public Result Confirm(LoRaConfirmType loRaConfirmType)
      {
         string command;

         switch (loRaConfirmType)
         {
            case LoRaConfirmType.Unconfirmed:
               command = "at+set_config=lora:confirm:0";
               break;
            case LoRaConfirmType.Confirmed:
               command = "at+set_config=lora:confirm:1";
               break;
            case LoRaConfirmType.Multicast:
               command = "at+set_config=lora:confirm:2";
               break;
            case LoRaConfirmType.Proprietary:
               command = "at+set_config=lora:confirm:3";
               break;
            default:
               throw new ArgumentException($"LoRa confirm type value {loRaConfirmType} invalid", nameof(loRaConfirmType));
         }

         // Set the confirmation type
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:confirm:{loRaConfirmType}");
#endif
         Result result = SendCommand("OK", command, CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:confirm failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }
      */

      public Result Band(string band)
      {
         // TODO Tweak validation so it works with AS923-1 etc. length 3 or five maybe
         //if (band.Length != RegionIDLength)
         //{
         //   throw new ArgumentException($"BandID {band} length {band.Length} invalid", nameof(band));
         //}

#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+BAND={band}");
#endif
         Result result = SendCommand("OK", $"AT+BAND={band}", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+BAND failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      /*
      public Result Sleep()
      {
         // Put the RAK module to sleep
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} device:sleep:1");
#endif
         Result result = SendCommand("OK Sleep", $"at+set_config=device:sleep:1", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} device:sleep failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result Wakeup()
      {
         // Wakeup the RAK Module
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} device:sleep:0");
#endif
         Result result = SendCommand("OK Wake Up", $"at+set_config=device:sleep:0", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} device:sleep failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }
      */

      public Result AdrOff()
      {
         // Adaptive Data Rate off
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=0");
#endif
         Result result = SendCommand("OK", $"AT+ADR=0", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=0 failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result AdrOn()
      {
         // Adaptive Data Rate on
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=1");
#endif
         Result result = SendCommand("OK", $"AT+ADR=1", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=1 {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      /*
      public Result AbpInitialise(string devAddr, string nwksKey, string appsKey)
      {
         Result result;

         if ((devAddr == null) || (devAddr.Length != DevAddrLength))
         {
            throw new ArgumentException($"devAddr invalid length must be {DevAddrLength} characters", nameof(devAddr));
         }
         if ((nwksKey == null) || (nwksKey.Length != NwsKeyLength))
         {
            throw new ArgumentException($"nwsKey invalid length must be {NwsKeyLength} characters", nameof(nwksKey));
         }
         if ((appsKey == null) || (appsKey.Length != AppsKeyLength))
         {
            throw new ArgumentException($"appsKey invalid length must be {AppsKeyLength} characters", nameof(appsKey));
         }

         // Set the JoinMode to ABP
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:join_mode:1");
#endif
         result = SendCommand("OK", $"at+set_config=lora:join_mode:1", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:join_mode failed {result}" );
#endif
            return result;
         }

         // set the devAddr
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:devAddr {devAddr}");
#endif
         result = SendCommand("OK", $"at+set_config=lora:dev_addr:{devAddr}", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:dev_addr failed {result}");
#endif
            return result;
         }

         // Set the nwsKey
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:nwks_Key:{nwksKey}");
#endif
         result = SendCommand("OK", $"at+set_config=lora:nwks_key:{nwksKey}", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:nwksKey failed {result}");
#endif
            return result;
         }

         // Set the appsKey
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:apps_key:{appsKey}");
#endif
         result = SendCommand("OK", $"at+set_config=lora:apps_key:{appsKey}", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:apps_key failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }
      */

      public Result OtaaInitialise(string appEui, string appKey)
      {
         Result result;

         if ((appEui == null) || (appEui.Length != AppEuiLength))
         {
            throw new ArgumentException($"appEui invalid length must be {AppEuiLength} characters", nameof(appEui));
         }
         if ((appKey == null) || (appKey.Length != AppKeyLength))
         {
            throw new ArgumentException($"appKey invalid length must be {AppKeyLength} characters", nameof(appKey));
         }

         // Set the JoinMode to OTAA
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NJM=1");
#endif
         result = SendCommand("OK", $"AT+NJM=1", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NJM=1 failed {result}");
#endif
            return result;
         }

         // Set the appEUI
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+APPEUI={appEui}");
#endif
         result = SendCommand("OK", $"AT+APPEUI={appEui}", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+APPEUI= failed {result}");
#endif
            return result;
         }

         // Set the appKey
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+APPKEY={appKey}");
#endif
         result = SendCommand("OK", $"AT+APPKEY={appKey}", CommandTimeoutDefault);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+APPKEY= failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result Join(TimeSpan timeout)
      {
         Result result;

         // Join the network
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} join");
#endif
         // TODO - Options 
         result = SendCommand("OK", $"AT+JOIN=1:0:10:2", timeout);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} join failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result Send(ushort port, string payload, TimeSpan timeout)
      {
         Result result;

         if ((port < MessagePortMinimumValue) || (port > MessagePortMaximumValue))
         {
            throw new ArgumentException($"port invalid must be greater than or equal to {MessagePortMinimumValue} and less than or equal to {MessagePortMaximumValue}", nameof(port));
         }

         if ((payload == null) || (payload.Length < MessageBcdMinimumLength) || (payload.Length > MessageBcdMaximumLength))
         {
            throw new ArgumentException($"payload invalid length must be greater than or equal to  {MessageBcdMinimumLength} and less than or equal to {MessageBcdMaximumLength} BCD characters long", nameof(payload));
         }

         // TODO timeout validation

         // Send message the network
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} Send port:{port} payload {payload} timeout {timeout.TotalSeconds} seconds");
#endif
         result = SendCommand("OK", $"AT+SEND={port}:{payload}", timeout);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} send failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result Send(ushort port, byte[] payloadBytes, TimeSpan timeout)
      {
         Result result;

         if ((port < MessagePortMinimumValue) || (port > MessagePortMaximumValue))
         {
            throw new ArgumentException($"port invalid must be greater than or equal to {MessagePortMinimumValue} and less than or equal to {MessagePortMaximumValue}", nameof(port));
         }

         if ((payloadBytes == null) || (payloadBytes.Length < MessageBytesMinimumLength) || (payloadBytes.Length > MessageBytesMaximumLength))
         {
            throw new ArgumentException($"payload invalid length must be greater than or equal to {MessageBytesMinimumLength} and less than or equal to {MessageBytesMaximumLength} bytes long", nameof(payloadBytes));
         }

         string payloadBcd = BytesToBcd(payloadBytes);

         // Send message the network
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} Send port:{port} payload {payloadBcd} timeout {timeout.TotalSeconds} seconds");
#endif
         result = SendCommand("OK", $"at+send=lora:{port}:{payloadBcd}", timeout);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} send failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      private Result SendCommand(string expectedResponse, string command, TimeSpan timeout)
      {
         if ((expectedResponse == null) || (expectedResponse == string.Empty))
         {
            throw new ArgumentException($"expectedResponse invalid length cannot be empty", nameof(expectedResponse));
         }
         if ((command == null) || (command == string.Empty))
         {
            throw new ArgumentException($"command invalid length cannot be empty", nameof(command));
         }

         this.atCommandExpectedResponse = expectedResponse;

         serialDevice.WriteLine(command);

         this.atExpectedEvent.Reset();

         if (!this.atExpectedEvent.WaitOne((int)timeout.TotalMilliseconds, false))
            return Result.ATResponseTimeout;

         this.atCommandExpectedResponse = string.Empty;

         return result;
      }

      private static Result ModemErrorParser(string errorText)
      {
         Result result;
         ushort errorNumber;
         //Debug.Assert((errorText == null) || (errorText == string.Empty));

         try
         {
            errorNumber = ushort.Parse(errorText);
         }
         catch (Exception)
         {
            return Result.ResponseInvalid;
         }

         switch (errorNumber)
         {
            case 1:
               result = Result.ATCommandUnsuported;
               break;
            case 2:
               result = Result.ATCommandInvalidParameter;
               break;
            case 3: //There is an error when reading or writing flash.
            case 4: //There is an error when reading or writing through IIC.
               result = Result.ErrorReadingOrWritingFlash;
               break;
            case 5: //There is an error when sending through UART
               result = Result.ATCommandInvalidParameter;
               break;
            case 41: //The BLE works in an invalid state, so that it can’t be operated.
               result = Result.ResponseInvalid;
               break;
            case 80:
               result = Result.LoRaBusy;
               break;
            case 81:
               result = Result.LoRaServiceIsUnknown;
               break;
            case 82:
               result = Result.LoRaParameterInvalid;
               break;
            case 83:
               result = Result.LoRaFrequencyInvalid;
               break;
            case 84:
               result = Result.LoRaDataRateInvalid;
               break;
            case 85:
               result = Result.LoRaFrequencyAndDataRateInvalid;
               break;
            case 86:
               result = Result.LoRaDeviceNotJoinedNetwork;
               break;
            case 87:
               result = Result.LoRaPacketToLong;
               break;
            case 88:
               result = Result.LoRaServiceIsClosedByServer;
               break;
            case 89:
               result = Result.LoRaRegionUnsupported;
               break;
            case 90:
               result = Result.LoRaDutyCycleRestricted;
               break;
            case 91:
               result = Result.LoRaNoValidChannelFound;
               break;
            case 92:
               result = Result.LoRaNoFreeChannelFound;
               break;
            case 93:
               result = Result.StatusIsError;
               break;
            case 94:
               result = Result.LoRaTransmitTimeout;
               break;
            case 95:
               result = Result.LoRaRX1Timeout;
               break;
            case 96:
               result = Result.LoRaRX2Timeout;
               break;
            case 97:
               result = Result.LoRaRX1ReceiveError;
               break;
            case 98:
               result = Result.LoRaRX2ReceiveError;
               break;
            case 99:
               result = Result.LoRaJoinFailed;
               break;
            case 100:
               result = Result.LoRaDownlinkRepeated;
               break;
            case 101:
               result = Result.LoRaPayloadSizeNotValidForDataRate;
               break;
            case 102:
               result = Result.LoRaTooManyDownlinkFramesLost;
               break;
            case 103:
               result = Result.LoRaAddressFail;
               break;
            case 104:
               result = Result.LoRaMicVerifyError;
               break;
            default:
               result = Result.ResponseInvalid;
               break;
         }

         return result;
      }

      private void SerialDevice_DataReceived(object sender, SerialDataReceivedEventArgs e)
      {
         SerialPort serialDevice = (SerialPort)sender;

         response.Append(serialDevice.ReadExisting());

         int eolPosition;
         do
         {
            // extract a line
            eolPosition = response.ToString().IndexOf(EndOfLineMarker);

            if (eolPosition != -1)
            {
               string line = response.ToString(0, eolPosition);
               response = response.Remove(0, eolPosition + EndOfLineMarker.Length);
#if DIAGNOSTICS
               Debug.WriteLine($" Line :{line} ResponseExpected:{atCommandExpectedResponse} Response:{response}");
#endif
               int joinSucessIndex = line.IndexOf(JoinMarkerSuccess);
               if (joinSucessIndex != -1)
               {
                  if (this.onJoinCompletion != null)
                  {
                     onJoinCompletion(true);
                  }
               }

               int joinFailureIndex = line.IndexOf(JoinMarkerFailure);
               if (joinFailureIndex != -1)
               {
                  if (this.onJoinCompletion != null)
                  {
                     OnMessageConfirmation(false);
                  }
               }

               int confirmedFailureIndex = line.IndexOf(ConfirmedMarkerFailure);
               if (joinFailureIndex != -1)
               {
                  if (this.onJoinCompletion != null)
                  {
                     OnMessageConfirmation(false);
                  }
               }

               int errorIndex = line.IndexOf(ErrorMarker);
               if (errorIndex != -1)
               {
                  string errorNumber = line.Substring(errorIndex + ErrorMarker.Length);

                  result = ModemErrorParser(errorNumber.Trim());
                  atExpectedEvent.Set();
               }

               if (atCommandExpectedResponse != string.Empty)
               {
                  int successIndex = line.IndexOf(atCommandExpectedResponse);
                  if (successIndex != -1)
                  {
                     result = Result.Success;
                     atExpectedEvent.Set();
                  }
               }

               int receivedMessageIndex = line.IndexOf(ReplyMarker);
               if (receivedMessageIndex != -1)
               {
                  string[] fields = line.Split("=,:".ToCharArray());

                  int port = int.Parse(fields[1]);
                  int rssi = int.Parse(fields[2]);
                  int snr = int.Parse(fields[3]);
                  int length = int.Parse(fields[4]);

                  if (this.OnMessageConfirmation != null)
                  {
                     //OnMessageConfirmation(rssi, snr);
                     OnMessageConfirmation(true);
                  }
                  if (length > 0)
                  {
                     string payload = fields[5];

                     if (this.OnReceiveMessage != null)
                     {
                        OnReceiveMessage(port, rssi, snr, payload);
                     }
                  }
               }
            }
         }
         while (eolPosition != -1);
      }

      // Utility functions for clients for processing messages payloads to be send, ands messages payloads received.
      public static string BytesToBcd(byte[] payloadBytes)
      {
         Debug.Assert(payloadBytes != null);
         Debug.Assert(payloadBytes.Length > 0);

         StringBuilder payloadBcd = new StringBuilder(BitConverter.ToString(payloadBytes));

         payloadBcd = payloadBcd.Replace("-", "");

         return payloadBcd.ToString();
      }

      public static byte[] BcdToByes(string payloadBcd)
      {
         Debug.Assert(payloadBcd != null);
         Debug.Assert(payloadBcd != String.Empty);
         Debug.Assert(payloadBcd.Length % 2 == 0);
         Byte[] payloadBytes = new byte[payloadBcd.Length / 2];

         char[] chars = payloadBcd.ToCharArray();

         for (int index = 0; index < payloadBytes.Length; index++)
         {
            byte byteHigh = Convert.ToByte(chars[index * 2].ToString(), 16);
            byte byteLow = Convert.ToByte(chars[(index * 2) + 1].ToString(), 16);

            payloadBytes[index] += (byte)(byteHigh * 16);
            payloadBytes[index] += byteLow;
         }

         return payloadBytes;
      }

      public void Dispose()
      {
         if (serialDevice != null)
         {
            serialDevice.Dispose();
            serialDevice = null;
         }
      }
   }
}
