using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;

namespace MobiFlight
{
    static class MobiFlightFirmwareUpdater
    {
        /***
         * "D:\portableapps\arduino-1.0.5\hardware\tools\avr\bin\avrdude"
         */
        public static String ArduinoIdePath { get; set; }
        public static String AvrPath { get { return "hardware\\tools\\avr"; } }

        /***
         * C:\\Users\\SEBAST~1\\AppData\\Local\\Temp\\build2060068306446321513.tmp\\cmd_test_mega.cpp.hex
         **/
        public static String FirmwarePath { get; set; }

        public static bool IsValidArduinoIdePath(string path)
        {
            return Directory.Exists(path + "\\" + AvrPath);
        }

        public static bool IsValidFirmwareFilepath(string filepath)
        {
            return File.Exists(filepath);
        }

        public static bool Update(MobiFlightModule module)
        {
            bool result = false;
            String Port = module.InitUploadAndReturnUploadPort();
            if (module.Connected) module.Disconnect();

            if (!(module.Board.Info.MobiFlightType == "MobiFlight RaspiPico"))
            {
                while (!SerialPort.GetPortNames().Contains(Port))           // Don't do this for Raspberry Pico!! COM Port changes while in Bootloader and needs more time!
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            else
            {
                System.Threading.Thread.Sleep(500);                         // wait for Bootloader to be ready for RaspiPico
            }


            if (module.Board.AvrDudeSettings != null)
            {
                try {
                    RunAvrDude(Port, module.Board);
                    result = true;
                } catch(Exception e) {
                    result = false;
                }

                if (module.Board.Connection.DelayAfterFirmwareUpdate > 0)
                {
                    System.Threading.Thread.Sleep(module.Board.Connection.DelayAfterFirmwareUpdate);
                }
            } else
            {
                Log.Instance.log($"Firmware update requested for {module.Board.Info.MobiFlightType} ({module.Port}) however no update settings were specified in the board definition file. Module update skipped.", LogSeverity.Warn);
            }
            return result;
        }

        public static void RunAvrDude(String Port, Board board) 
        {
            String FirmwareName = board.AvrDudeSettings.GetFirmwareName(board.Info.LatestFirmwareVersion);
            if (board.Info.MobiFlightType == "MobiFlight RaspiPico")
            {
                FirmwareName = FirmwareName.Replace("hex", "elf");
            } else if (board.Info.MobiFlightType == "MobiFlight BluePill")
            {
                FirmwareName = FirmwareName.Replace("hex", "elf");
            }
            String ArduinoChip = board.AvrDudeSettings.Device;
            String Bytes = board.AvrDudeSettings.BaudRate;
            String C = board.AvrDudeSettings.Programmer;
            String message = "";

             if (!IsValidFirmwareFilepath(FirmwarePath + "\\" + FirmwareName))
            {
                message = "Firmware not found: " + FirmwarePath + "\\" + FirmwareName;
                Log.Instance.log(message, LogSeverity.Error);
                throw new FileNotFoundException(message);
            }

            String verboseLevel = "";

            //verboseLevel = " -v -v -v -v";

            var proc1 = new ProcessStartInfo();
            string anyCommand = "";

            if (board.Info.MobiFlightType == "MobiFlight RaspiPico")
            {
                String FullRaspiPicoUpdatePath = Directory.GetCurrentDirectory() + "\\RaspberryPico\\tools";
                anyCommand = verboseLevel + "-v -D " + FirmwarePath + "\\" + FirmwareName;
                proc1.WorkingDirectory = FullRaspiPicoUpdatePath;
                proc1.FileName = "\"" + FullRaspiPicoUpdatePath + "\\rp2040load.exe" + "\"";
                proc1.Arguments = anyCommand;
                //proc1.WindowStyle = ProcessWindowStyle.Hidden;
                Log.Instance.log("RunRaspberryPicoUpdater : " + proc1.FileName, LogSeverity.Info);
                Log.Instance.log("RunRaspberryPicoUpdater : " + anyCommand, LogSeverity.Info);
            }
            else if (board.Info.MobiFlightType == "MobiFlight Teensy35" || board.Info.MobiFlightType == "MobiFlight Teensy41")
            {
                String FullTeensyUpdatePath = Directory.GetCurrentDirectory() + "\\Teensy\\tools";
                anyCommand = verboseLevel + "-file=" + FirmwareName + " -path=" + "\"" + FirmwarePath + "\"" + " -tools=" + FullTeensyUpdatePath + " -board=" + ArduinoChip + " -reboot";
                proc1.WorkingDirectory = FullTeensyUpdatePath;
                proc1.FileName = "\"" + FullTeensyUpdatePath + "\\teensy_post_compile.exe" + "\"";
                proc1.Arguments = anyCommand;
                //proc1.WindowStyle = ProcessWindowStyle.Hidden;
                Log.Instance.log("RunTeensyUpdater : " + proc1.FileName, LogSeverity.Info);
                Log.Instance.log("RunTeensyUpdater : " + anyCommand, LogSeverity.Info);
            } else if (board.Info.MobiFlightType == "MobiFlight BluePill")
            {
                // maple_upload COM33 2 1EAF:0003 "C:\_PlatformIO\MobiFlight-Firmware\.pio\build\bluepill\firmware.bin"
                String FullBluePillUpdatePath = Directory.GetCurrentDirectory() + "\\STM32duino\\tools";
                anyCommand = "/c maple_upload.bat " + verboseLevel + Port + " 2 1EAF:003 " + FirmwarePath + "\\" + FirmwareName;
                proc1.WorkingDirectory = FullBluePillUpdatePath;
                proc1.FileName = "\"" + FullBluePillUpdatePath + "\\cmd.exe" + "\"";
                proc1.Arguments = anyCommand;
                //    proc1.WindowStyle = ProcessWindowStyle.Hidden;
                Log.Instance.log("RunBluePillUpdater : " + proc1.FileName, LogSeverity.Info);
                Log.Instance.log("RunBluePillUpdater : " + anyCommand, LogSeverity.Info);

                return;              // Flash Commands to be added
            } else
            {
                String FullAvrDudePath = $@"{ArduinoIdePath}\{AvrPath}";

                anyCommand =
                    $@"-C""{FullAvrDudePath}\etc\avrdude.conf"" {verboseLevel} -p{ArduinoChip} -c{C} -P\\.\{Port} -b{Bytes} -D -Uflash:w:""{FirmwarePath}\{FirmwareName}"":i";
                proc1.UseShellExecute = true;
                proc1.WorkingDirectory = $@"""{FullAvrDudePath}""";
                proc1.FileName = $@"""{FullAvrDudePath}\bin\avrdude""";
                proc1.Arguments = anyCommand;
                proc1.WindowStyle = ProcessWindowStyle.Hidden;
                Log.Instance.log("RunAvrDude : " + proc1.FileName, LogSeverity.Debug);
                Log.Instance.log("RunAvrDude : " + anyCommand, LogSeverity.Debug);
            }
            Process p = Process.Start(proc1);
            if (p.WaitForExit(board.AvrDudeSettings.Timeout))
            {
                Log.Instance.log("Firmware Upload Exit Code: " + p.ExitCode, LogSeverity.Info);
                // everything OK
                if (p.ExitCode == 0) return;
                
                // process terminated but with an error.
                message = $"ExitCode: {p.ExitCode} => Something went wrong when flashing with command \n {proc1.FileName} {anyCommand}";
            } else
            {
                // we timed out;
                p.Kill();
                message = $"avrdude timed out! Something went wrong when flashing with command \n {proc1.FileName} {anyCommand}";
            }

            Log.Instance.log(message, LogSeverity.Error);
            throw new Exception(message);
        }
    }
}
