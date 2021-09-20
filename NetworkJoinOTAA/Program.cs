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
namespace devMobile.IoT.NetCore.RAK3172.NetworkJoinOTAA
{
	using System;
	using System.Diagnostics;
	using System.IO.Ports;
	using System.Threading;

	public class Program
	{
		private const string SerialPortId = "/dev/ttyS0";
		private const string AppEui = "...";
		private const string AppKey = "...";
		private const byte MessagePort = 1;
		private const string Payload = "A0EEE456D02AFF4AB8BAFD58101D2A2A"; // Hello LoRaWAN

		public static void Main()
		{
			string response;

			Debug.WriteLine("devMobile.IoT.NetCore.Rak3172.NetworkJoinOTAA starting");

			Debug.WriteLine(String.Join(",", SerialPort.GetPortNames()));

			try
			{
				using (SerialPort serialPort = new SerialPort(SerialPortId))
				{
					// set parameters
					serialPort.BaudRate = 9600;
					serialPort.DataBits = 8;
					serialPort.Parity = Parity.None;
					serialPort.StopBits = StopBits.One;
					serialPort.Handshake = Handshake.None;

					serialPort.ReadTimeout = 5000;

					serialPort.NewLine = "\r\n";

					serialPort.Open();

					// clear out the RX buffer
					response = serialPort.ReadExisting();
					Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");
					Thread.Sleep(500);

					// Set the Working mode to LoRaWAN
					Console.WriteLine("Set Work mode");
					serialPort.WriteLine("AT+NWM=1");
					// Read the blank line
					response = serialPort.ReadLine();
					// Read the response
					response = serialPort.ReadLine();
					Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");

					// Set the Region to AS923
					Console.WriteLine("Set Region");
					serialPort.WriteLine("AT+BAND=8-1");
					// Read the blank line
					response = serialPort.ReadLine();
					// Read the response
					response = serialPort.ReadLine();
					Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");

					// Set the JoinMode
					Console.WriteLine("Set Join mode");
					serialPort.WriteLine("AT+NJM=1");
					// Read the blank line
					response = serialPort.ReadLine();
					// Read the response
					response = serialPort.ReadLine();
					Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");

					// Set the appEUI
					Console.WriteLine("Set App Eui");
					serialPort.WriteLine($"AT+APPEUI={AppEui}");
					// Read the blank line
					response = serialPort.ReadLine();
					// Read the response
					response = serialPort.ReadLine();
					Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");

					// Set the appKey
					Console.WriteLine("Set App Key");
					serialPort.WriteLine($"AT+APPKEY={AppKey}");
					// Read the blank line
					response = serialPort.ReadLine();
					// Read the response
					response = serialPort.ReadLine();
					Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");

					// Set the Confirm flag
					Console.WriteLine("Set Confirm off");
					serialPort.WriteLine("AT+CFM=0");
					// Read the blank line
					response = serialPort.ReadLine();
					// Read the response
					response = serialPort.ReadLine(); 
					Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");

					// Join the network
					Console.WriteLine("Start Join");
					serialPort.WriteLine("AT+JOIN=1:0:10:2");

					// Read the blank line
					response = serialPort.ReadLine();

					// Read the Result
					response = serialPort.ReadLine();
					Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");

					Thread.Sleep(10000);

					// Read the +EVT:JOINED
					response = serialPort.ReadLine();
					Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");

					while (true)
					{
						Console.WriteLine("Sending");
						serialPort.WriteLine($"AT+SEND={MessagePort}:{Payload}");

						// Read the blank line
						response = serialPort.ReadLine();

						// Read the result
						Console.WriteLine("Send result");
						response = serialPort.ReadLine();
						Debug.WriteLine($"RX :{response.Trim()} bytes:{response.Length}");

						Thread.Sleep(300000);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}
	}
}
