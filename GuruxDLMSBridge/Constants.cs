//
// --------------------------------------------------------------------------
//  AISE, s.r.o.
//
//
//
// Filename:        Constants
//
// Version:         $Revision$,
//                  $Date$
//                  Author: Holík, Pilz
//
// Copyright (c) AISE, s.r.o.
//
//---------------------------------------------------------------------------
//
//  DESCRIPTION
//
// This class is used to store global constants.
//
// This file is based on Gurux Device Framework.
//
// Gurux Device Framework is Open Source software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; version 2 of the License.
// Gurux Device Framework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
//
// More information of Gurux products: http://www.gurux.org
//
// This code is licensed under the GNU General Public License v2.
// Full text may be retrieved at http://www.gnu.org/licenses/gpl-2.0.txt
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gurux.DLMS.Client.Example
{
    static public class Constants
    {
        //Gurux argumenty
        //GuruxDLMSBridge.exe - S COM4: 9600:8None1 - c 32 - n 72980038 - a Low - P 00000000 - w 1 - t Verbose - o C:\Projekty\C##\E360.XML -g "1.0.72.7.0.255:2"
        //GuruxDLMSBridge.exe - S COM4: 9600:8None1 - c 32 - n 65963674 - a Low - P 00000000 - w 1 - t Verbose - o C:\Projekty\C##\E570.XML -g "1.0.72.7.0.255:2"

        // slouží jen pro testovací účely, plně nahrazenou funkcionalitou fcí přijímajících proměnné z komunikace a čtením txt souboru měřidla
        //For testing purposes only, fully replaced by functions accepting variables from communication and reading the meter's TXT file.
        public static readonly string[] ARGS =
        {
            /*sfArgs[0] =*/"-S",
            /*sfArgs[1] =*/"COM8:9600:8None1", //"COM4:9600:8None1",
            /*sfArgs[2] =*/"-c",
            /*sfArgs[3] =*/"32",
            /*sfArgs[4] =*/ "-n",
            /*sfArgs[5] =*/ "72980038",
            /*sfArgs[6] = */"-a",
            /*sfArgs[7] =*/ "Low",
            /*sfArgs[8] =*/ "-P",
            /*sfArgs[9] =*/ "00000000",
            /*sfArgs[10] = */"-w",
            /*sfArgs[11] = */"1",
            /*sfArgs[12] =*/ "-t",
            /*sfArgs[13] =*/ "Verbose",
            /*sfArgs[14] =*/ "-o",
            /* sfArgs[15] =*/ "C:\\Projekty\\C##\\E360.XML",
            /*sfArgs[16] =*/ "-g",
            /*sfArgs[17] = */"0.0.40.0.0.255:2" //"1.0.72.7.0.255:2"
        };

        // settings from zpa code
        public const byte NO_ERROR = 0;
        public const byte NO_ASSOCIATION = 1;
        public const byte OBIS_NOT_EXIST = 2;
        public const byte NO_SAMPLE_IN_INTERVAL = 3;
        public const byte BAD_COMMAND_SYNTAX = 4;
        public const byte NO_OBJ_ATTR = 5;
        public const byte UNIMPLEMETED_COMMAND = 7;
        public const byte UKNOWN_REASON = 255;

        
        public const byte TYP_INT8 = 0;
        public const byte TYP_BYTE8 = 1;
        public const byte TYP_WORD16 = 2;
        public const byte TYP_INT16 = 3;
        public const byte TYP_INT32 = 4;
        public const byte TYP_DWORD32 = 5;
        public const byte TYP_DOUBLE = 6;
        public const byte TYP_STRING = 7;
        public const byte TYP_TIME = 8;

        // default setting common arguments variable 
        public const byte VSIZERXTX = 1;

        // default nastavení výrobce pro pojmenování souboru např. LGZ72980038.XML
        //Default manufacturer setting for file naming, e.g., LGZ72980038.XML.
        public const string PRODUCER = "LGZ";

        // AuthenticationTypes jsou přednastavené hodnoty pro mapování configurace v ini 
        //[None1]
        //SecurityLevel="None"
        //Password="00000000"
        //ClientAddress=32

        //[None]
        //SecurityLevel="High"
        //Password="12548962"
        //ClientAddress=64

        // settings options (None, Low, High, HighMd5, HighSha1, HighGMac, HighSha256)");
        public static readonly string[,] AuthenticationTypes =
           {    {"0", "None", "None"},
                {"1","None1", "None"},
                {"20","Low", "Low"},
                {"40","High","High"},
                {"60","HighMd","HighMd5"},
                {"80","HighSha", "HighSha1"},
                {"100","HighGMac", "HighGMac"},
                {"120","HighShaSha", "HighSha256"},
            };

    }
}
