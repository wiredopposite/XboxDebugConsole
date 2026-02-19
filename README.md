# XboxDebugConsole
XboxDebugConsole is a command line tool used to debug original Xbox applications over LAN. It features a JSON-only mode (launched with ```XboxDebugConsole json```) for easy integration with other tools.

This tool is still in early development and may contain bugs or incomplete features. VsCode debugger integration is being worked on and is high priority currently.

## Requirements
- An original Xbox running:
	- Xbox Development Kit (XDK) dashboard
	- xbdm.dll version 4831 (or newer, can be obtained by unpacking an XDK PC installer exe, ```xbox/symbols/4831/xbdm.dll```)
	- A debug BIOS

## Commands
This list is incomplete and subject to change as development continues.
```scan timeoutMs=<MILLISECONDS>``` - Scans the local network for Xbox consoles and displays their IP addresses and names. ```timeoutMs``` is optional and specifies the maximum time in milliseconds to wait for responses, default is 5000.
```connect``` - Connects to the first Xbox console found on the local network.
```connect name=<CONSOLE_NAME>``` - Connects to the Xbox console with the specified name.
```connect ip=<IP_ADDRESS>``` - Connects to the Xbox console at the specified IP address.
```disconnect``` - Disconnects from the currently connected Xbox console.
```reboot autoReconnect=<BOOL> timeoutMs=<MILLISECONDS>``` - Cold reboot the connected Xbox console. ```autoReconnect``` is optional, if ```true```, the console will automatically attempt to reconnect after rebooting. ```timeoutMs``` is optional and specifies the maximum time in milliseconds to wait for the console to reconnect, default is 5000.
```upload localPath=<PATH> remotePath=<PATH>``` - Upload a file from the local machine to the Xbox console. ```localPath``` is the path to the file on the local machine, and ```remotePath``` is the desired path on the Xbox console.
```launch remotePath=<PATH>``` - Launch an application on the Xbox console from the specified remote file path.
```launchdash``` - Launch the Xbox dashboard.
```setbreak address=<HEXADDRESS>``` - Set a breakpoint at the specified memory address.
```deletebreak address=<HEXADDRESS>``` - Clear a breakpoint at the specified memory address.
```listbreaks``` - List all currently set breakpoints.
```stop``` - Pause execution of the currently running Xbox application.
```continue``` - Continue execution of the currently paused Xbox application.
```read address=<HEXADDRESS> length=<LENGTH>``` - Read a block of memory of the specified length in bytes from the specified memory address.
```dump address=<HEXADDRESS> lenght=<LENGTH> localPath=<PATH>``` - Dump a block of memory of the specified length in bytes, from the specified memory address, to a file at the specified path.
```write address=<HEXADDRESS> data=<BYTES>``` - Write a block of data to the specified memory address. The data should be provided as a hexadecimal string.
```modules``` - List all loaded modules on the Xbox console, including their base addresses and sizes.
```threads``` - List all active threads on the Xbox console, including their thread IDs and statuses.
```registers threadId=<THREAD_ID>``` - Display the CPU registers for the specified thread ID. If no thread ID is provided, the first found will be used. 
```regions``` - List all memory regions on the Xbox console, including their base addresses, sizes, and permissions.
```exit``` or ```quit``` - Exit the XboxDebugConsole application.
```help``` - Display a list of all available commands with their descriptions and usage instructions.