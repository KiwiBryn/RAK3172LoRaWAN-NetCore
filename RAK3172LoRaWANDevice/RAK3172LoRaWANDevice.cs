﻿//---------------------------------------------------------------------------------
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

	/// <summary>
	/// 
	/// </summary>
	public enum LoRaClass
	{
		Undefined = 0,
		A,
		B,
		C
	}

	/// <summary>
	/// 
	/// </summary>
	public enum LoRaConfirmType
	{
		Undefined = 0,
		Unconfirmed,
		Confirmed,
	}

	/// <summary>
	/// 
	/// </summary>
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

	/// <summary>
	/// 
	/// </summary>
	public sealed class Rak3172LoRaWanDevice : IDisposable
	{
		public const ushort BaudRateMinimum = 600;
		public const ushort BaudRateMaximum = 57600;
		public const ushort DevEuiLength = 16;
		public const ushort AppEuiLength = 16;
		public const ushort AppKeyLength = 32;
		public const ushort DevAddrLength = 8;
		public const ushort NwsKeyLength = 32;
		public const ushort AppsKeyLength = 32;
		public const ushort MessagePortMinimumValue = 1;
		public const ushort MessagePortMaximumValue = 223;
		public const ushort JoinRetryIntervalMinimum = 7;

		private SerialPort serialDevice = null;
		private const int CommandTimeoutDefaultmSec = 1500;
		private readonly Thread processModuleResponsesThread = null;
		private Boolean processModuleResponses = true;
		private readonly AutoResetEvent atExpectedEvent;
		private Result result;

		public delegate void JoinCompletionHandler(bool result);
		public JoinCompletionHandler OnJoinCompletion;
		public delegate void MessageConfirmationHandler();
		public MessageConfirmationHandler OnMessageConfirmation;
		public delegate void ReceiveMessageHandler(int port, int rssi, int snr, string payload);
		public ReceiveMessageHandler OnReceiveMessage;

		public Rak3172LoRaWanDevice()
		{
			this.processModuleResponsesThread = new Thread(SerialPortProcessor);

			this.atExpectedEvent = new AutoResetEvent(false);
		}

		/// <summary>
		/// Initializes a new instance of the devMobile.IoT.LoRaWAN.NetCore.RAK3172.Rak3172LoRaWanDevice class using the
		//     specified port name, baud rate, parity bit, data bits, and stop bit.
		/// </summary>
		/// <param name="serialPortId">The port to use (for example, COM1).</param>
		/// <param name="baudRate">The baud rate, 600 to 57K6.</param>
		/// <param name="serialParity">One of the System.IO.Ports.SerialPort.Parity values, defaults to None.</param>
		/// <param name="dataBits">The data bits value, defaults to 8.</param>
		/// <param name="stopBits">One of the System.IO.Ports.SerialPort.StopBits values, defaults to One.</param>
		/// <exception cref="System.IO.IOException">The specified port could not be found or opened.</exception>
		/// <exception cref="System.IO.ArgumentNullException">The specified serialPortId serialPortId is null.</exception>
		/// <exception cref="System.IO.ArgumentException">The specified baud rate is invalid.</exception>
		/// <returns>One of the devMobile.IoT.LoRaWAN.NetCore.RAK3172.Result values </returns>
		public Result Initialise(string serialPortId, int baudRate, Parity serialParity = Parity.None, ushort dataBits = 8, StopBits stopBits = StopBits.One)
		{
			if (serialPortId == null)
			{
				throw new ArgumentNullException(nameof(serialPortId));
			}
			if (serialPortId == string.Empty)
			{
				throw new ArgumentException("SerialPortId invalid", nameof(serialPortId));
			}
			if ((baudRate < BaudRateMinimum) || (baudRate > BaudRateMaximum))
			{
				throw new ArgumentException("Invalid BaudRate must be {BaudRateMinimum} too {BaudRateMaximum} baud  ", nameof(baudRate));
			}

			serialDevice = new SerialPort(serialPortId);

			// set SerialPort parameters
			serialDevice.BaudRate = baudRate;
			serialDevice.Parity = serialParity;
			serialDevice.DataBits = dataBits;
			serialDevice.StopBits = stopBits;
			serialDevice.Handshake = Handshake.None;

			serialDevice.NewLine = "\r\n";

			serialDevice.ReadTimeout = CommandTimeoutDefaultmSec;

			serialDevice.Open();
			serialDevice.ReadExisting();

			processModuleResponsesThread.Start();

			// Set the Working mode to LoRaWAN
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NWM=1");
#endif
			Result result = SendCommand("AT+NWM=1");
			if (result != Result.Success)
			{
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NWM=1 failed {result}");
#endif
				return result;
			}

			return Result.Success;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="loRaClass"></param>
		/// <returns></returns>

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
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} {command}");
#endif
			Result result = SendCommand(command);
			if (result != Result.Success)
			{
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} {command} failed {result}");
#endif
				return result;
			}

			return Result.Success;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="loRaConfirmType"></param>
		/// <returns></returns>
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
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} {command}");
#endif
			Result result = SendCommand(command);
			if (result != Result.Success)
			{
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} {command} failed {result}");
#endif
				return result;
			}

			return Result.Success;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="band"></param>
		/// <returns></returns>
		public Result Band(string band)
		{
			if (band == null)
			{
				throw new ArgumentNullException(nameof(band), $"Band is invalid");
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

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public Result AdrOff()
		{
			// Adaptive Data Rate off
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=0");
#endif
			Result result = SendCommand("AT+ADR=0");
			if (result != Result.Success)
			{
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=0 failed {result}");
#endif
				return result;
			}

			return Result.Success;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public Result AdrOn()
		{
			// Adaptive Data Rate on
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=1");
#endif
			Result result = SendCommand("AT+ADR=1");
			if (result != Result.Success)
			{
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+ADR=1 failed {result}");
#endif
				return result;
			}

			return Result.Success;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="devAddr"></param>
		/// <param name="nwksKey"></param>
		/// <param name="appsKey"></param>
		/// <returns></returns>
		public Result AbpInitialise(string devAddr, string nwksKey, string appsKey)
		{
			Result result;

			if (devAddr == null)
			{
				throw new ArgumentNullException(nameof(devAddr));
			}

			if (devAddr.Length != DevAddrLength)
			{
				throw new ArgumentException($"devAddr invalid length must be {DevAddrLength} characters", nameof(devAddr));
			}

			if (nwksKey == null)
			{
				throw new ArgumentNullException(nameof(nwksKey));
			}

			if (nwksKey.Length != NwsKeyLength)
			{
				throw new ArgumentException($"nwksKey invalid length must be {NwsKeyLength} characters", nameof(nwksKey));
			}

			if (appsKey == null)
			{
				throw new ArgumentNullException(nameof(appsKey));
			}

			if (appsKey.Length != AppsKeyLength)
			{
				throw new ArgumentException($"appsKey invalid length must be {AppsKeyLength} characters", nameof(appsKey));
			}

			// Set the network join mode to ABP
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NJM=0");
#endif
			result = SendCommand("AT+NJM=0");
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="appEui"></param>
		/// <param name="appKey"></param>
		/// <returns></returns>
		public Result OtaaInitialise(string appEui, string appKey)
		{
			Result result;

			if (appEui == null)
			{
				throw new ArgumentNullException(nameof(appEui));
			}

			if (appEui.Length != AppEuiLength)
			{
				throw new ArgumentException($"appEui invalid length must be {AppEuiLength} characters", nameof(appEui));
			}

			if (appKey == null)
			{
				throw new ArgumentNullException(nameof(appKey));
			}

			if (appKey.Length != AppKeyLength)
			{
				throw new ArgumentException($"appKey invalid length must be {AppKeyLength} characters", nameof(appKey));
			}

			// Set the Network Join Mode to OTAA
#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+NJM=1");
#endif
			result = SendCommand("AT+NJM=1");
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="JoinAttempts"></param>
		/// <param name="retryInterval"></param>
		/// <returns></returns>
		public Result Join(ushort JoinAttempts = 0, ushort retryInterval = 8)
		{
			if (retryInterval < JoinRetryIntervalMinimum)
			{
				throw new ArgumentException($"retryInterval invalid must be > {JoinRetryIntervalMinimum} seconds", nameof(retryInterval));
			}

#if DIAGNOSTICS
         Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+JOIN");
#endif
			Result result = SendCommand($"AT+JOIN=1:0:{retryInterval}:{JoinAttempts}");
			if (result != Result.Success)
			{
#if DIAGNOSTICS
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+JOIN failed {result}");
#endif
				return result;
			}

			return Result.Success;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="port"></param>
		/// <param name="payload"></param>
		/// <returns></returns>
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

			if ((payload.Length % 2) != 0)
			{
				throw new ArgumentException("Payload length invalid must be a multiple of 2", nameof(payload));
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="port"></param>
		/// <param name="payload"></param>
		/// <returns></returns>
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
            Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} AT+SEND failed {result}");
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

			serialDevice.WriteLine(command);

			this.atExpectedEvent.Reset();

			if (!this.atExpectedEvent.WaitOne(CommandTimeoutDefaultmSec, false))
				return Result.Timeout;

			return result;
		}

		private void SerialPortProcessor()
		{
			string line;

			while (processModuleResponses)
			{
				try
				{ 
#if DIAGNOSTICS
					Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} ReadLine before");
#endif
					line = serialDevice.ReadLine();
#if DIAGNOSTICS
					Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} ReadLine after:{line}");
#endif

					// See if device successfully joined network
					if (line.StartsWith("+EVT:JOINED"))
					{
						OnJoinCompletion?.Invoke(true);

						continue;
					}

					// See if device failed ot join network
					if (line.StartsWith("+EVT:JOIN FAILED"))
					{
						OnJoinCompletion?.Invoke(false);

						continue;
					}

					// Applicable only if confirmed messages enabled 
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

	#if DIAGNOSTICS
						Debug.WriteLine($"{DateTime.UtcNow:HH:mm:ss} UNICAST :{line}");
	#endif
						line = serialDevice.ReadLine();
	#if DIAGNOSTICS
						Debug.WriteLine($"{DateTime.UtcNow:HH:mm:ss} Payload:{line}");
	#endif
						string[] fields2 = line.Split(':');

						int port = int.Parse(fields2[1]);
						string payload = fields2[2];

						OnReceiveMessage?.Invoke(port, rssi, snr, payload);

						continue;
					}

#if DIAGNOSTICS
               Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} ReadLine Result");
#endif
					line = serialDevice.ReadLine();
#if DIAGNOSTICS
               Debug.WriteLine($" {DateTime.UtcNow:hh:mm:ss} ReadLine Result:{line}");
#endif
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

					atExpectedEvent.Set();
				}
				catch (TimeoutException)
				{
					// Intentionally ignored, not certain this is a good idea
				}
			}
		}

		// Utility functions for clients for processing messages payloads to be send, ands messages payloads received.
		/// <summary>
		/// 
		/// </summary>
		/// <param name="payloadBytes"></param>
		/// <returns></returns>
		public static string BytesToBcd(byte[] payloadBytes)
		{
			Debug.Assert(payloadBytes != null);

			StringBuilder payloadBcd = new StringBuilder(BitConverter.ToString(payloadBytes));

			payloadBcd = payloadBcd.Replace("-", "");

			return payloadBcd.ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="payloadBcd"></param>
		/// <returns></returns>
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
			processModuleResponses = false;
			processModuleResponsesThread.Join();

			if (serialDevice != null)
			{
				serialDevice.Dispose();
				serialDevice = null;
			}
		}
	}
}
