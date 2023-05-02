using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Security;

namespace PalaLite.Models
{
    #region enum define

    public enum FindCellMode
    {
        Analysis = 0,
        SingleCell,
        Enrichment,
    }


    // Motion Control for Plate control
    public enum PlateCtl
    {
        Home_X = 0,
        Home_Y,
        Home,
        Home_X_Safe,
        Home_Y_Safe,
        Door,
        A1,
        Up,
        Down,
        Left,
        Right,
        Waste,
        Bulk,
    }

    // Camera Resolution
    public enum CamResolution
    {
        R32_x_32 = 0,
        R64_x_64,
        R160_x_120,
        R320_x_240,
        R640_x_480,
        R752_x_480,
        R800_x_600,
        R1024_x_768,
        R1280_x_1024,
        R2048_x_1536,
        R2592_x_1944,
    }

    public enum NM_MessageBoxIcon
    {
        None = 0,
        Error,
        Question,
        Exclamation,
        Information,
        Success,
    }

    #endregion

    #region Struct define

    // Overall System Config
    public struct SystemConfig
    {
        public int ValveDelay;
        public int ValveSpike;
        public int ValveHold;
        public int ValvePulse;
        public int ValveGap;
        public int Average;
        public int FSC1Threshold;
        public int FSC2Threshold;
        public int ALL1Threshold;
        public int PulseMin;
        public int PulseMax;
        public int PulseGap;
        public byte SSCThresholdW;
        public int SheathPressure;
        public int SamplePressure;
        public int WastePressure;
        public int BulkWastePressure;
        public int FSCGain;
        public int SSCGain;
        public int PMT1Gain;
        public int PMT2Gain;
        public int PMT3Gain;
        public int PMT4Gain;
        public int PMT5Gain;
        public int PMT6Gain;
        public int SheathL;
        public int SheathH;
        public int WasteL;
        public int WasteH;
        public int ServoPosition1;
        public int ServoPosition2;
        public int ServoStallC;
        public int ServoStallT;
        public bool WasteSensorOn;
        public bool ShowSetting;
        public int BulkValve;
        public int BulkSpike;
        public int BulkDelay;
        public int Laser;

    }

    // Camera configuration
    public struct CamConfig
    {
        public int m_iExposure_ms;
        public int m_iGain;
        public int m_iCameraBuffer;
        public CamResolution m_eResolution;
        public int m_iROIX;
        public int m_iROIY;
    }

    // Cell Analysis Data
    public struct CellPMTData //Don't be confused with CellData from namocellApi
    {
        public Single FSC1H; //first laser
        public Single FSC1W;
        public Single FSC2H; //second laser
        public Single FSC2W;
        public Single SSC1H;
        public Single FL11; //first laser
        public Single FL12; 
        public Single FL13;
        public Single FL14;
        public Single FL15;
        public Single FL16;
        public Single FL21; //second laser
        public Single FL22;
        public Single FL23;
        public Single FL24;
        public Single FL25;
        public Single FL26;

        public int EventCount;
        public uint Time;
        public int Sort;
        public string Row;
        public string Column;

        public override string ToString()
        {
            var msg = string.Format("{0},{1}", EventCount, Time);
            return msg;
        }
    }

    #endregion

    public class CNM_Def
    {
        public const string STRINIFile = "Namocell.ini";
        public const string STRExtTif = ".tif";        // image file (tif) extension

        public static string[] STRROW_Alphabet = { "A", "B", "C", "D", "E", "F", "G", "H", "I",
                                             "J", "K", "L", "M", "N", "O", "P", "Q", "R", 
                                             "S", "T", "U", "V", "W", "X", "Y", "Z"};

        #region OS Clock Setting DLL
        /// <summary>TimeBeginPeriod(). See the Windows API documentation for details.</summary>

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage"), SuppressUnmanagedCodeSecurity]
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]

        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        /// <summary>TimeEndPeriod(). See the Windows API documentation for details.</summary>

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage"), SuppressUnmanagedCodeSecurity]
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]

        public static extern uint TimeEndPeriod(uint uMilliseconds);
        #endregion

    }
}
