# PalaLite
Lightweight data streamer for Pala development.

## Getting started
### Clone the project
`git clone https://github.com/ProteinSimple/Pala.git`
### Restore Nuget Packages
`msbuild PalaLite.sln -t:restore -p:RestorePackagesConfig=true`

## Usage
### Initialization
Upon initial powerup, the simulator must be initialized.  Run the `Pala` app, and run `Analysis` until events appear.

Once events have appeared, stop `Analysis` and shut down the program.

### Running
The number of packets to be analyzed can be changed by editing `_packetsToAnalyze` in `Program.cs`.

Data files are saved in `C:\ProgramData\Namocell\Logs` in the form `<DateStr>_PalaLite.csv`

## Signal Generator
The signal generator frequency can be adjusted with the [Serial Monitor Utility](https://serialport.en.softonic.com/?ex=DINS-635.1).

Connect to the correct COM Port with `Baudrate=115200`.

To change the frequency, send messages with the format `E<Num>`, where `<Num>` is four digits.  The target frequency is (2*Num)/10

For example, `E0050 == 10Hz`, `E2500 == 500Hz`, `E4500 == 900Hz`
