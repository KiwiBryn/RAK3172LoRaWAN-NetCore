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
	using System.Threading;

	public class Program
	{
		private static SerialPort serialPort;
#if SERIAL_THREADED_READ
		private static Boolean _continue = true;
#endif
		private const string SerialPortId = "/dev/ttyS0";

		public static void Main()
		{
#if SERIAL_THREADED_READ
			Thread readThread = new Thread(SerialPortProcessor);
#endif

			Debug.WriteLine("devMobile.IoT.LoRaWAN.NetCore.RAK3172 BreakoutSerial starting");

			Debug.WriteLine(String.Join(",", SerialPort.GetPortNames()));

			try
			{
				serialPort = new SerialPort(SerialPortId);

				// set parameters
				serialPort.BaudRate = 9600;
				serialPort.Parity = Parity.None;
				serialPort.DataBits = 8;
				serialPort.StopBits = StopBits.One;
				serialPort.Handshake = Handshake.None;

				serialPort.ReadTimeout = 1000;

				serialPort.NewLine = "\r\n";

				serialPort.Open();

#if SERIAL_THREADED_READ
				readThread.Start();
#endif

#if SERIAL_ASYNC_READ
				serialPort.DataReceived += SerialDevice_DataReceived;
#endif

				while (true)
				{
					serialPort.WriteLine("AT+VER=?");

#if READLINE_BUFFER
					serialPort.WriteLine("AT+VER=?");
#endif

#if SERIAL_SYNC_READ
					// Read the response
					string response = serialPort.ReadLine();
					Debug.WriteLine($"RX:{response.Trim()} bytes:{response.Length}");

					// Read the blank line
					response = serialPort.ReadLine();
					Debug.WriteLine($"RX:{response.Trim()} bytes:{response.Length}");

					// Read the result
					response = serialPort.ReadLine();
					Debug.WriteLine($"RX:{response.Trim()} bytes:{response.Length}");

	#if READLINE_BUFFER
					message = serialPort.ReadLine();
					Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} 3:{message}");

					message = serialPort.ReadExisting();
					Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} 4:{message}");
	#endif
#endif
					Thread.Sleep(20000);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}

#if SERIAL_ASYNC_READ
		private static void SerialDevice_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			SerialPort serialPort = (SerialPort)sender;

			switch (e.EventType)
			{
				case SerialData.Chars:
					string response = serialPort.ReadExisting();

					Debug.WriteLine($"RX:{response.Trim()} bytes:{response.Length}");
					break;

				case SerialData.Eof:
					Debug.WriteLine("RX :EoF");
					break;
				default:
					Debug.Assert(false, $"e.EventType {e.EventType} unknown");
					break;
			}
		}
#endif

#if SERIAL_THREADED_READ
		public static void SerialPortProcessor()
		{
			string message;

			while (_continue)
			{
				try
				{
					message = serialPort.ReadLine();
					Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} 1:{message}");

					message = serialPort.ReadLine();
					Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} 2:{message}");

#if READLINE_BUFFER
					message = serialPort.ReadLine();
					Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} 3:{message}");

					message = serialPort.ReadLine();
					Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} 4:{message}");
#endif
					message = serialPort.ReadLine();
					Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} 5:{message}");

					message = serialPort.ReadLine();
					Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} 6:{message}");

				}
				catch (TimeoutException) 
				{
					message = serialPort.ReadExisting();
					Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} Timeout:{message}");
				}
			}
		}
#endif
	}
}