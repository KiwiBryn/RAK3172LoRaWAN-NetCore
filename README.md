# RAK3172LoRaWAN-NetCore - WARNING This library is for an retired firmware
C# RAK3172 LoRaWAN library for .Net Core powered devices
A C# library for [LoRaWAN](https://lora-alliance.org/about-lorawan) connectivity for [RAKwireless](https://www.rakwireless.com/en-us) [RAK3172 Module](https://www.rakwireless.com/en-us/products/lpwan-modules/rak3172-wisduo-lpwan-module) equipped [.Net Core 5](https://dotnet.microsoft.com/) powered devices.

This repo has the source code for the series of blog posts written as I work on the library

01. [Basic connectivity](http://blog.devmobile.co.nz/2021/09/19/net-core-rak3172-lorawan-library-part1/)
02. [Nasty OTAA connect](http://blog.devmobile.co.nz/2021/09/20/net-core-rak3172-lorawan-library-part2/)
03. [Nasty ABP connect](https://blog.devmobile.co.nz/2021/09/20/net-core-rak3172-lorawan-library-part3/)
04. [Starting again with Threads](https://blog.devmobile.co.nz/2021/09/27/net-core-rak3172-lorawan-library-part4/)
05. [The massive refactor](https://blog.devmobile.co.nz/2021/10/03/net-core-rak3172-lorawan-library-part5/)

I used a [Grove Base HAT for Raspberry Pi](https://www.seeedstudio.com/Grove-Base-Hat-for-Raspberry-Pi.html) and [RAK3172 Breakout](https://store.rakwireless.com/products/wisduo-breakout-board-rak3272s) for development

![Grove Base Hat for RaspberryPI connected RAK3127](RRPI3Rak3172.jpg)

To use the breakout board with a [Grove Base Hat](https://www.seeedstudio.com/Grove-Base-Hat-for-Raspberry-Pi.html) connect the GND, 3V3, UART2_RX and UART2_TX pins.

![Close up of Grove cable connection to RAK3172 breakout board](BreakoutConnection.jpg)

[The Things Network](https://www.thethingsnetwork.org/) uplink and downlink messaging 
![TTN Uplink and downlink messaging](TTIConnectAndSend.PNG)

I used a local [RAK7258 WisGate Edge Lite](https://store.rakwireless.com/collections/wisgate-edge/products/rak7258-micro-gateway) connected to [The Things Network](https://www.thethingsnetwork.org/) to test my implementation.
