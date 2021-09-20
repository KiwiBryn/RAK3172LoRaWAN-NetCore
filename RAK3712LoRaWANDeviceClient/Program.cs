//---------------------------------------------------------------------------------
// Copyright (c) May 2021, devMobile Software
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
//    ST_STM32F769I_DISCOVERY or (workin on other device support)
//
// Optional definitions
//    CONFIRMED For confirmed messages
//    RESET for retun device to factory settings
//
// To download nanoBooter and nanoCLR to device
//    nanoff --target ST_STM32F769I_DISCOVERY --update
//---------------------------------------------------------------------------------
namespace devMobile.IoT.RAK3712LoRaWANDeviceClient
{
	using System;

	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");
		}
	}
}
