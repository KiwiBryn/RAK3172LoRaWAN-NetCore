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
//    RESET for retun device to factory settings
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
		private const string Region = "8-1";
		private static readonly TimeSpan JoinTimeOut = new TimeSpan(0, 0, 10);
		private static readonly TimeSpan SendTimeout = new TimeSpan(0, 0, 10);
		private const byte MessagePort = 1;
#if PAYLOAD_BCD
		private const string PayloadBcd = "48656c6c6f204c6f526157414e"; // Hello LoRaWAN in BCD
#endif
#if PAYLOAD_BYTES
      private static readonly byte[] PayloadBytes = { 0x48, 0x65, 0x6c, 0x6c, 0x6f, 0x20, 0x4c, 0x6f, 0x52, 0x61, 0x57, 0x41, 0x4e}; // Hello LoRaWAN in bytes
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

#if CONFIRMED
               device.OnMessageConfirmation += OnMessageConfirmationHandler;
#endif
					device.OnReceiveMessage += OnReceiveMessageHandler;
					device.onJoinCompletion += onJoinCompletionHandler;

					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Band {Region}");
					result = device.Band(Region);
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

					// TODO
/*
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
*/

#if OTAA
					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} OTAA");
					result = device.OtaaInitialise(Config.AppEui, Config.AppKey);
					if (result != Result.Success)
					{
						Debug.WriteLine($"OTAA Initialise failed {result}");
						return;
					}
#endif

					/*
#if ABP
               Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} ABP");
               result = device.AbpInitialise(DevAddress, NwksKey, AppsKey);
               if (result != Result.Success)
               {
                  Debug.WriteLine($"ABP Initialise failed {result}");
                  return;
               }
#endif
					*/

					//Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join start Timeout:{JoinTimeOut:hh:mm:ss}");
					result = device.Join(JoinTimeOut);
					if (result != Result.Success)
					{
						Debug.WriteLine($"Join failed {result}");
						return;
					}
					Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join finish");

					while (true)
					{
#if PAYLOAD_BCD
						//Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Send Timeout:{SendTimeout:hh:mm:ss} port:{MessagePort} payload BCD:{PayloadBcd}");
						result = device.Send(MessagePort, PayloadBcd, SendTimeout);
#endif
#if PAYLOAD_BYTES
                  Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Send Timeout:{SendTimeout:hh:mm:ss} port:{MessagePort} payload Bytes:{BitConverter.ToString(PayloadBytes)}");
                  result = device.Send(MessagePort, PayloadBytes, SendTimeout);
#endif
						if (result != Result.Success)
						{
							Debug.WriteLine($"Send failed {result}");
						}

						/*
						// if we sleep module too soon response is missed
						Thread.Sleep(new TimeSpan(0,0,5));

						Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Sleep");
						result = device.Sleep();
						if (result != Result.Success)
						{
							Debug.WriteLine($"Sleep failed {result}");
							return;
						}
						*/
						//Thread.Sleep(new TimeSpan(0, 5, 0));
						Thread.Sleep(new TimeSpan(0, 0, 30));
						/*
						Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Wakeup");
						result = device.Wakeup();
						if (result != Result.Success)
						{
							Debug.WriteLine($"Wakeup failed {result}");
							return;
						}

						// if we send too soon after wakeup issues
						Thread.Sleep(new TimeSpan(0, 0, 10));
						*/
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}

		static void onJoinCompletionHandler(bool result)
		{
			if (result)
			{
				Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join successful");
			}
			else
			{
				Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Join failed");
			}
		}

#if CONFIRMED
		//static void OnMessageConfirmationHandler(int rssi, int snr)
		static void OnMessageConfirmationHandler(bool result)
      {
			//Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Send Confirm RSSI:{rssi} SNR:{snr}");
			if (result)
			{
				Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Send successful");
			}
			else
			{
				Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Send failed");
			}
		}
#endif

		static void OnReceiveMessageHandler(int port, int rssi, int snr, string payloadBcd)
		{
			byte[] payloadBytes = Rak3172LoRaWanDevice.BcdToByes(payloadBcd);

			Debug.WriteLine($"{DateTime.UtcNow:hh:mm:ss} Receive Message RSSI:{rssi} SNR:{snr} Port:{port} Payload:{payloadBcd} PayLoadBytes:{BitConverter.ToString(payloadBytes)}");
		}
	}
}
