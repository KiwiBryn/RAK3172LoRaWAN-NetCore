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
// Must have one of following options defined in the nfproj file
//    PAYLOAD_BCD or PAYLOAD_BYTES
//    OTAA or ABP
//
// Optional definitions
//    CONFIRMED For confirmed messages
//
//---------------------------------------------------------------------------------
namespace devMobile.IoT.LoRaWAN.NetCore.RAK3172
{
	using System;
	using System.Threading;
	using System.Diagnostics;
	using System.IO.Ports;

	public class Program
	{
		private const string SerialPortId = "/dev/ttyS0";
		private const LoRaClass Class = LoRaClass.A;
		private const string Band = "8-1";
		private const byte MessagePort = 10;
		private static readonly TimeSpan JoinTimeOut = new TimeSpan(0, 0, 30);
		private static readonly TimeSpan MessageSentTimerDue = new TimeSpan(0, 0, 15);
		private static readonly TimeSpan MessageSentTimerPeriod = new TimeSpan(0, 5, 0);
		private static Timer SendTimer ;
#if PAYLOAD_BCD
		private const string PayloadBcd = "48656c6c6f204c6f526157414e"; // Hello LoRaWAN in BCD
#endif
#if PAYLOAD_BYTES
		private static readonly byte[] PayloadBytes = { 0x65 , 0x6c, 0x6c, 0x6f, 0x20, 0x4c, 0x6f, 0x52, 0x61, 0x57, 0x41, 0x4e}; // Hello LoRaWAN in bytes
#endif

		public static void Main()
		{
			Result result;

			Debug.WriteLine("devMobile.IoT.LoRaWAN.NetCore.RAK3172 RAK3712LoRaWANDeviceClient starting");

			Debug.WriteLine(String.Join(",", SerialPort.GetPortNames()));

			try
			{
				using (Rak3172LoRaWanDevice device = new Rak3172LoRaWanDevice())
				{
					result = device.Initialise(SerialPortId, 9600, Parity.None, 8, StopBits.One);
					if (result != Result.Success)
					{
						Debug.WriteLine($"Initialise failed {result}");
						return;
					}

					SendTimer = new Timer(SendMessageTimerCallback, device,Timeout.Infinite, Timeout.Infinite);

					device.OnJoinCompletion += OnJoinCompletionHandler;
					device.OnReceiveMessage += OnReceiveMessageHandler;
#if CONFIRMED
					device.OnMessageConfirmation += OnMessageConfirmationHandler;
#endif

					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Class {Class}");
					result = device.Class(Class);
					if (result != Result.Success)
					{
						Debug.WriteLine($"Region failed {result}");
						return;
					}


					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Band {Band}");
					result = device.Band(Band);
					if (result != Result.Success)
					{
						Debug.WriteLine($"Region failed {result}");
						return;
					}

					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} ADR On");
					result = device.AdrOn();
					if (result != Result.Success)
					{
						Debug.WriteLine($"ADR on failed {result}");
						return;
					}

#if CONFIRMED
               Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Confirmed");
               result = device.Confirm(LoRaConfirmType.Confirmed);
               if (result != Result.Success)
               {
                  Debug.WriteLine($"Confirm on failed {result}");
                  return;
               }
#else
					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Unconfirmed");
					result = device.Confirm(LoRaConfirmType.Unconfirmed);
					if (result != Result.Success)
					{
						Debug.WriteLine($"Confirm off failed {result}");
						return;
					}
#endif

#if OTAA
					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} OTAA");
					result = device.OtaaInitialise(Config.AppEui, Config.AppKey);
					if (result != Result.Success)
					{
						Debug.WriteLine($"OTAA Initialise failed {result}");
						return;
					}
#endif

#if ABP
               Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} ABP");
               result = device.AbpInitialise(Config.DevAddress, Config.NwksKey, Config.AppsKey);
               if (result != Result.Success)
               {
                  Debug.WriteLine($"ABP Initialise failed {result}");
                  return;
               }
#endif

					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join start");
					result = device.Join(JoinTimeOut);
					if (result != Result.Success)
					{
						Debug.WriteLine($"Join failed {result}");
						return;
					}
					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join started");

					Thread.Sleep(Timeout.Infinite);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}

		static void OnJoinCompletionHandler(bool result)
		{
			Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join result:{result}");

			if (result)
			{ 
				SendTimer.Change(MessageSentTimerDue, MessageSentTimerPeriod);
			}
		}

		static void SendMessageTimerCallback(object state)
		{
			Rak3172LoRaWanDevice device = (Rak3172LoRaWanDevice)state;

#if PAYLOAD_BCD
			Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} port:{MessagePort} payload BCD:{PayloadBcd}");
			Result result = device.Send(MessagePort, PayloadBcd );
#endif
#if PAYLOAD_BYTES
			Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} port:{MessagePort}");
         Result result = device.Send(MessagePort, PayloadBytes);
#endif
			if (result != Result.Success)
			{
				Debug.WriteLine($"Send failed {result}");
			}
		}

#if CONFIRMED
		static void OnMessageConfirmationHandler()
      {
			Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Send successful");
		}
#endif

		static void OnReceiveMessageHandler(int port, int rssi, int snr, string payloadBcd)
		{
			byte[] payloadBytes = Rak3172LoRaWanDevice.BcdToByes(payloadBcd);

			Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Receive Message RSSI:{rssi} SNR:{snr} Port:{port} Payload:{payloadBcd} PayLoadBytes:{BitConverter.ToString(payloadBytes)}");
		}
	}
}
