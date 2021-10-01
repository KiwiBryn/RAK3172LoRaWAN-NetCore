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
	using System.IO.Ports;
	using System.Threading;


	public class Program
	{
		private const string SerialPortId = "/dev/ttyS0";
		private const LoRaClass Class = LoRaClass.A;
		private const string Band = "8-1";
		private const byte MessagePort = 10;
		private static readonly TimeSpan MessageSendTimerDue = new TimeSpan(0, 0, 15);
		private static readonly TimeSpan MessageSendTimerPeriod = new TimeSpan(0, 5, 0);
		private static Timer MessageSendTimer ;
		private const int JoinRetryAttempts = 2;
		private const int JoinRetryIntervalSeconds = 10;
#if PAYLOAD_BCD
		private const string PayloadBcd = "48656c6c6f204c6f526157414e"; // Hello LoRaWAN in BCD
#endif
#if PAYLOAD_BYTES
		private static readonly byte[] PayloadBytes = { 0x65 , 0x6c, 0x6c, 0x6f, 0x20, 0x4c, 0x6f, 0x52, 0x61, 0x57, 0x41, 0x4e}; // Hello LoRaWAN in bytes
#endif

		public static void Main()
		{
			Result result;

			Console.WriteLine("devMobile.IoT.LoRaWAN.NetCore.RAK3172 RAK3712LoRaWANDeviceClient starting");

			Console.WriteLine($"Serial ports:{String.Join(",", SerialPort.GetPortNames())}");

			try
			{
				using (Rak3172LoRaWanDevice device = new Rak3172LoRaWanDevice())
				{
					result = device.Initialise(SerialPortId, 9600, Parity.None, 8, StopBits.One);
					if (result != Result.Success)
					{
						Console.WriteLine($"Initialise failed {result}");
						return;
					}

					MessageSendTimer = new Timer(SendMessageTimerCallback, device,Timeout.Infinite, Timeout.Infinite);

					device.OnJoinCompletion += OnJoinCompletionHandler;
					device.OnReceiveMessage += OnReceiveMessageHandler;
#if CONFIRMED
					device.OnMessageConfirmation += OnMessageConfirmationHandler;
#endif

					Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Class {Class}");
					result = device.Class(Class);
					if (result != Result.Success)
					{
						Console.WriteLine($"Class failed {result}");
						return;
					}

					Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Band {Band}");
					result = device.Band(Band);
					if (result != Result.Success)
					{
						Console.WriteLine($"Region failed {result}");
						return;
					}

					Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} ADR On");
					result = device.AdrOn();
					if (result != Result.Success)
					{
						Console.WriteLine($"ADR on failed {result}");
						return;
					}

#if CONFIRMED
               Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Confirmed");
               result = device.Confirm(LoRaConfirmType.Confirmed);
               if (result != Result.Success)
               {
                  Console.WriteLine($"Confirm on failed {result}");
                  return;
               }
#else
					Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Unconfirmed");
					result = device.Confirm(LoRaConfirmType.Unconfirmed);
					if (result != Result.Success)
					{
						Console.WriteLine($"Confirm off failed {result}");
						return;
					}
#endif

#if OTAA
					Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} OTAA");
					result = device.OtaaInitialise(Config.AppEui, Config.AppKey);
					if (result != Result.Success)
					{
						Console.WriteLine($"OTAA Initialise failed {result}");
						return;
					}
#endif

#if ABP
               Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} ABP");
               result = device.AbpInitialise(Config.DevAddress, Config.NwksKey, Config.AppsKey);
               if (result != Result.Success)
               {
                  Console.WriteLine($"ABP Initialise failed {result}");
                  return;
               }
#endif

					Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join start");
					result = device.Join(JoinRetryAttempts, JoinRetryIntervalSeconds);
					if (result != Result.Success)
					{
						Console.WriteLine($"Join failed {result}");
						return;
					}
					Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join started");

					Thread.Sleep(Timeout.Infinite);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		private static void OnJoinCompletionHandler(bool result)
		{
			Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join finished:{result}");

			if (result)
			{ 
				MessageSendTimer.Change(MessageSendTimerDue, MessageSendTimerPeriod);
			}
		}

		private static void SendMessageTimerCallback(object state)
		{
			Rak3172LoRaWanDevice device = (Rak3172LoRaWanDevice)state;

#if PAYLOAD_BCD
			Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} port:{MessagePort} payload BCD:{PayloadBcd}");
			Result result = device.Send(MessagePort, PayloadBcd );
#endif
#if PAYLOAD_BYTES
			Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} port:{MessagePort} payload bytes:{Rak3172LoRaWanDevice.BytesToBcd(PayloadBytes)}");
         Result result = device.Send(MessagePort, PayloadBytes);
#endif
			if (result != Result.Success)
			{
				Console.WriteLine($"Send failed {result}");
			}
		}

#if CONFIRMED
		private static void OnMessageConfirmationHandler()
      {
			Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Send successful");
		}
#endif

		private static void OnReceiveMessageHandler(int port, int rssi, int snr, string payloadBcd)
		{
			byte[] payloadBytes = Rak3172LoRaWanDevice.BcdToByes(payloadBcd); // Done this way so both 

			Console.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Receive Message RSSI:{rssi} SNR:{snr} Port:{port} Payload:{payloadBcd} PayLoadBytes:{BitConverter.ToString(payloadBytes)}");
		}
	}
}
