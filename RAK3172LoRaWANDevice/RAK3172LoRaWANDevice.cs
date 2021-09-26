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
   }

   public enum Result
   {
      Undefined = 0,
      Success,
      Timeout,
      Error,
      ParameterError,
      BusyError,
      ParameterOverflow,
      NotJoined,
      ReceiveError,
      DutyCycleRestricted
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

      private readonly TimeSpan CommandTimeoutDefault = new TimeSpan(0, 0, 3);

      private SerialPort serialDevice = null;
      private readonly Thread readThread = null;

      private readonly AutoResetEvent atExpectedEvent;
      private Result result;

      public delegate void JoinCompletionHandler(bool result);
      public JoinCompletionHandler onJoinCompletion;
      public delegate void MessageConfirmationHandler();
      public MessageConfirmationHandler OnMessageConfirmation;
      public delegate void ReceiveMessageHandler(int port, int rssi, int snr, string payload);
      public ReceiveMessageHandler OnReceiveMessage;
      public delegate void JoinHandler(bool success);
      public JoinHandler OnJoinCompletion;


      public Rak3172LoRaWanDevice()
      {
         this.readThread = new Thread(SerialPortProcessor);

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

         serialDevice.ReadTimeout = 5000;

         serialDevice.Open();
         serialDevice.ReadExisting();

         readThread.Start();

         // Set the Working mode to LoRaWAN
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:work_mode LoRaWAN");
#endif
         result = SendCommand("AT+NWM=1");
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
         Result result = SendCommand(command);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+CLASS failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result Confirm(LoRaConfirmType loRaConfirmType)
      {
         string command;

         switch (loRaConfirmType)
         {
            case LoRaConfirmType.Unconfirmed:
               command = "AT+CFM=0";
               break;
            case LoRaConfirmType.Confirmed:
               command = "AT+CFM=1";
               break;
            default:
               throw new ArgumentException($"LoRa confirm type value {loRaConfirmType} invalid", nameof(loRaConfirmType));
         }

         // Set the confirmation type
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:confirm:{loRaConfirmType}");
#endif
         Result result = SendCommand(command);
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} lora:confirm failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result Band(string band)
      {
         if (band == null)
         {
            throw new  ArgumentNullException(nameof(band), $"Band is invalid");
         }

#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+BAND={band}");
#endif
         Result result = SendCommand($"AT+BAND={band}");
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+BAND failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result AdrOff()
      {
         // Adaptive Data Rate off
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=0");
#endif
         Result result = SendCommand($"AT+ADR=0");
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
         Result result = SendCommand($"AT+ADR=1");
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=1 {result}");
#endif
            return result;
         }

         return Result.Success;
      }

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

         // Set the network join mode to ABP
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NJM=0");
#endif
         result = SendCommand($"AT+NJM=0");
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NJM=0 failed {result}" );
#endif
            return result;
         }

         // set the devAddr
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+DEVADDR={devAddr}");
#endif
         result = SendCommand($"AT+DEVADDR={devAddr}");
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+DEVADDR failed {result}");
#endif
            return result;
         }

         // Set the nwsKey
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NWKSKEY={nwksKey}");
#endif
         result = SendCommand($"AT+NWKSKEY={nwksKey}");
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NWKSKEY failed {result}");
#endif
            return result;
         }

         // Set the appsKey
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+APPSKEY={appsKey}");
#endif
         result = SendCommand($"AT+APPSKEY={appsKey}");
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+APPSKEY failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

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

         // Set the Network Join Mode to OTAA
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NJM=1");
#endif
         result = SendCommand($"AT+NJM=1");
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
         result = SendCommand($"AT+APPEUI={appEui}");
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
         result = SendCommand($"AT+APPKEY={appKey}");
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
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+JOIN");
#endif
         // TODO - Options 
         result = SendCommand($"AT+JOIN=1:0:10:2");
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+JOIN failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result Send(ushort port, string payload)
      {
         if ((port < MessagePortMinimumValue) || (port > MessagePortMaximumValue))
         {
            throw new ArgumentException($"Port invalid must be greater than or equal to {MessagePortMinimumValue} and less than or equal to {MessagePortMaximumValue}", nameof(port));
         }

         if (payload == null)
         {
            throw new ArgumentNullException(nameof(payload));
         }

         if ((payload.Length %2)==0)
         {
            throw new ArgumentException($"Payload invalid length must be a multiple of 2", nameof(payload));
         }

         // Send message the network
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+SEND={port}:payload {payload}");
#endif
         Result result = SendCommand($"AT+SEND={port}:{payload}");
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+SEND failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      public Result Send(ushort port, byte[] payload)
      {
         if ((port < MessagePortMinimumValue) || (port > MessagePortMaximumValue))
         {
            throw new ArgumentException($"Port invalid must be greater than or equal to {MessagePortMinimumValue} and less than or equal to {MessagePortMaximumValue}", nameof(port));
         }

         if (payload == null)
         {
            throw new ArgumentNullException(nameof(payload));
         }

         string payloadBcd = BytesToBcd(payload);

         // Send message the network
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+SEND=:{port} payload {payloadBcd}");
#endif
         Result result = SendCommand($"AT+SEND={port}:{payloadBcd}");
         if (result != Result.Success)
         {
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} send failed {result}");
#endif
            return result;
         }

         return Result.Success;
      }

      private Result SendCommand(string command)
      {
         if (command == null)
			{
            throw new ArgumentNullException(nameof(command));
         }

         if (command == string.Empty)
         {
            throw new ArgumentException($"command invalid length cannot be empty", nameof(command));
         }

         serialDevice.ReadTimeout = (int)CommandTimeoutDefault.TotalMilliseconds;
         serialDevice.WriteLine(command);

         this.atExpectedEvent.Reset();

         if (!this.atExpectedEvent.WaitOne((int)CommandTimeoutDefault.TotalMilliseconds, false))
            return Result.Timeout;

         return result;
      }

      public void SerialPortProcessor()
      {
         string line;

         while (true)
         {
            this.serialDevice.ReadTimeout = -1;

            Debug.WriteLine("ReadLine before");
            line = serialDevice.ReadLine();
            Debug.WriteLine($"ReadLine after:{line}");

            // check for +EVT:JOINED
            if (line.StartsWith("+EVT:JOINED"))
            {
					OnJoinCompletion?.Invoke(true);

               continue;
            }

            if (line.StartsWith("+EVT:JOIN FAILED"))
            {
					OnJoinCompletion?.Invoke(false);

               continue;
            }

            if (line.StartsWith("+EVT:SEND CONFIRMED OK"))
            {
               OnMessageConfirmation?.Invoke();

               continue;
            }

            // Check for A/B/C downlink message
            if (line.StartsWith("+EVT:RX_1") || line.StartsWith("+EVT:RX_2") || line.StartsWith("+EVT:RX_3") || line.StartsWith("+EVT:RX_C"))
            {
               string[] fields1 = line.Split(' ', ',');

               int rssi = int.Parse(fields1[3]);
               int snr = int.Parse(fields1[6]);
 
               line = serialDevice.ReadLine();
               Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} UNICAST :{line}");

               line = serialDevice.ReadLine();
               Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} Payload:{line}");

               string[] fields2 = line.Split(':');

               int port = int.Parse(fields2[1]);
               string payload = fields2[2];

					OnReceiveMessage?.Invoke(port, rssi, snr, payload);

               continue;
            }

            try
            {
               this.serialDevice.ReadTimeout = 3000;

               Debug.WriteLine("ReadLine Result");
               line = serialDevice.ReadLine();
               Debug.WriteLine($"ReadLine Result after:{line}");

               switch (line)
               {
                  case "OK":
                     result = Result.Success;
                     break;
                  case "AT_ERROR":
                     result = Result.Error;
                     break;
                  case "AT_PARAM_ERROR":
                     result = Result.ParameterError;
                     break;
                  case "AT_BUSY_ERROR":
                     result = Result.BusyError;
                     break;
                  case "AT_TEST_PARAM_OVERFLOW":
                     result = Result.ParameterOverflow;
                     break;
                  case "AT_NO_NETWORK_JOINED":
                     result = Result.NotJoined;
                     break;
                  case "AT_RX_ERROR":
                     result = Result.ReceiveError;
                     break;
                  case "AT_DUTYCYLE_RESTRICTED":
                     result = Result.DutyCycleRestricted;
                     break;
                  default:
                     result = Result.Undefined;
                     break;
               }
            }
            catch (TimeoutException) 
            {
               result = Result.Timeout;
            }

            atExpectedEvent.Set();
         }
      }

      // Utility functions for clients for processing messages payloads to be send, ands messages payloads received.
      public static string BytesToBcd(byte[] payloadBytes)
      {
         Debug.Assert(payloadBytes != null);

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
         if (readThread != null)
         {
            readThread.Join();
         }

         if (serialDevice != null)
         {
            serialDevice.Dispose();
            serialDevice = null;
         }
      }
   }
}
