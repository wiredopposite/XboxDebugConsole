# XboxDebugConsole
XboxDebugConsole is a command line tool used to debug original Xbox applications over LAN. It features a JSON-only mode for easy integration with other tools.

This tool is still in early development and may contain bugs or incomplete features. VsCode debugger integration is being worked on.

## Requirements
- An original Xbox running:
	- Xbox Development Kit (XDK) dashboard
	- xbdm.dll version 4831 (or newer, can be obtained by unpacking an XDK PC installer exe, ```xbox/symbols/4831/xbdm.dll```)
	- A debug BIOS

## Commands
This list is subject to change as development continues.

Formatting:
- ```Command``` Description
  - ```Parameter``` Description

Launch params:
- ```json``` Optional. Launches the app in JSON-only mode, where all output is in JSON format for easy parsing by other tools.
- ```mute``` Optional. Mutes the notifications from the Xbox on launch.

Commands:
- ```scan``` Scans the network for available Xbox consoles.
    - ```timeoutMs``` Optional. Time in milliseconds to wait for responses. Default is 5000.
- ```connect``` Connects to an Xbox console by IP address, name, or first available.
    - ```ip``` Optional. IP address of the console to connect to.
    - ```name```  Optional. Name of the console to connect to.
    - ```timeoutMs``` Optional. Time in milliseconds to wait for connection. Default is 5000.
- ```disconnect``` Disconnects from the currently connected Xbox console.
- ```mute``` Mutes the notifications from the Xbox.
- ```unmute``` Unmutes the notifications from the Xbox.
- ```loadsymbols``` Loads symbols from a PDB file for better debugging.
    - ```pdbPath``` Required. Local path to the PDB file.
    - ```imageBase``` Optional. Image base address to load symbols at.
- ```setbreak``` Sets breakpoints at specified addresses or source lines.
    - ```address``` Required if no symbols loaded. Address to set a breakpoint at.
    - ```file``` Required if no address provided. Source file for setting a breakpoint.
    - ```line```  Required if no address provided. Line number in the source file for the breakpoint.
- ```deletebreak``` Sets breakpoints at specified addresses or source lines.
    - ```address``` Required if no symbols loaded. Address to set a breakpoint at.
    - ```file``` Required if no address provided. Source file for setting a breakpoint.
    - ```line``` Required if no address provided. Line number in the source file for the breakpoint.
- ```pause``` Pauses the execution of the program.
- ```resume``` Resumes the execution of the program.
- ```read``` Reads memory from the Xbox at a specified address.
    - ```address```  Required. Address to read memory from.
    - ```length``` Required. Number of bytes to read.
- ```dump``` Dumps memory from the Xbox at a specified address to a local file.
    - ```address``` Required. Address to dump memory from.
    - ```length``` Required. Number of bytes to dump.
    - ```localPath```  Required. Local path to save the dumped memory to.
- ```write``` Writes memory to the Xbox at a specified address.
    - ```address```  Required. Address to write memory to.
    - ```data``` Required. Hex string of bytes to write.
- ```threads``` Lists all active threads.
- ```registers``` Retrieves the register values for a specific thread.
    - ```threadId``` Optional. ID of the thread to get registers for. Default is first available.
- ```modules``` Lists all loaded modules.
- ```regions``` Lists all loaded memory regions.
- ```upload``` Uploads a local file to the Xbox.
    - ```localPath``` Required. Local path of the file to upload.
    - ```remotePath``` Required. Remote path on the Xbox to upload the file to.
- ```launch``` Launches an application on the Xbox.
    - ```remotePath``` Required. Remote path of the application on the Xbox to launch.
- ```reboot``` Reboots the Xbox console.
    - ```autoReconnect``` Optional. Whether to automatically reconnect after reboot. Default is false.
    - ```timeoutMs``` Optional. Time in milliseconds to wait for the console to come back online. Default is 10000.
- ```quit``` or ```exit``` Quits the application.
- ```help``` or ```?``` Displays help/usage message.

### Usage
Launch the app:
```bash
XboxDebugConsole
```
Scan for Xboxes:
```bash
  scan timeoutMs=5000
```
Will return a list of available Xboxes on the network:
```bash
  Type: scan
  Result: True
  Message: null
  Payload:
    [0]:
      name: myxbox1
      ip: 192.168.0.1
    [1]:
      name: myxbox2
      ip: 192.168.0.2
```
Connect to an Xbox:
```bash
  connect ip=192.168.0.1
```
or
```bash
  connect name=myxbox1
```
