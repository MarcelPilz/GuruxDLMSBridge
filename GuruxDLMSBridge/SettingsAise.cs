// --------------------------------------------------------------------------
//  AISE, s.r.o.
//
//
//
// Filename:        $HeadURL$
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
// The methods of this class are used to populate variables and call Gurux setting methods.
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
using System.Text;
using Gurux.Common;
using Gurux.Serial;
using Gurux.Net;
using Gurux.DLMS.Enums;
using Gurux.DLMS.Secure;
using System.Diagnostics;
using System.IO.Ports;
using Gurux.DLMS.Objects.Enums;
using Gurux.MQTT;
using System.Runtime;

namespace Gurux.DLMS.Client.Example
{
    //  nastavení jednotlivých parametrů měřidla podle funčních bloků
    public static class SettingsAise
    {
        // nastaví přístupové parametry jako jsou heslo, úroven zabezpečení a adresy serveru a klienta
        public static void GetAccessInfo(Settings settings, int iSerialNumber, int iClientNumber, string sAuth, string sPassword)
        {
           
            //-n 72980038
            if (iSerialNumber != -1) // pokud bylo zadáno seriové číslo
                settings.client.ServerAddress = GXDLMSClient.GetServerAddressFromSerialNumber(iSerialNumber, 1);
            else// pokud NEbylo zadáno seriové číslo
                settings.client.ServerAddress = GXDLMSClient.GetServerAddress(1, settings.client.ServerAddress);

            //-c 32
            settings.client.ClientAddress = iClientNumber;

            //-a Low
            try
            {
                if (string.Compare("None", sAuth, true) == 0)
                {
                    settings.client.Authentication = Authentication.None;
                }
                else if (string.Compare("Low", sAuth, true) == 0)
                {
                    settings.client.Authentication = Authentication.Low;
                }
                else if (string.Compare("High", sAuth, true) == 0)
                {
                    settings.client.Authentication = Authentication.High;
                }
                else if (string.Compare("HighMd5", sAuth, true) == 0)
                {
                    settings.client.Authentication = Authentication.HighMD5;
                }
                else if (string.Compare("HighSha1", sAuth, true) == 0)
                {
                    settings.client.Authentication = Authentication.HighSHA1;
                }
                else if (string.Compare("HighSha256", sAuth, true) == 0)
                {
                    settings.client.Authentication = Authentication.HighSHA256;
                }
                else if (string.Compare("HighGMac", sAuth, true) == 0)
                {
                    settings.client.Authentication = Authentication.HighGMAC;
                }
                else if (string.Compare("HighECDSA", sAuth, true) == 0)
                {
                    settings.client.Authentication = Authentication.HighECDSA;
                }
                else
                {
                    throw new ArgumentException("Invalid Authentication option: '" + sAuth + "'. (None, Low, High, HighMd5, HighSha1, HighGMac, HighSha256)");
                }
            }
            catch (Exception)
            {
                throw new ArgumentException("Invalid Authentication option: '" + sAuth + "'. (None, Low, High, HighMd5, HighSha1, HighGMac, HighSha256)");
            }

            //- P 00000000
            if (sPassword.StartsWith("0x"))
            {
                settings.client.Password = GXCommon.HexToBytes(sPassword.Substring(2));
            }
            else
            {
                settings.client.Password = ASCIIEncoding.ASCII.GetBytes(sPassword);
            }
        }

        // nastaví default parametry pro WindowSizeRX TX, trace level, outfile - path to xml
        public static void GetCommonSetting(Settings settings, byte bWSize, Enum enTraceLevel, string sOutFile)
        {
            // -w 1  
            settings.client.HdlcSettings.WindowSizeRX = settings.client.HdlcSettings.WindowSizeTX = bWSize;

            // n- t Verbose
            try
            {
                settings.trace = (TraceLevel)enTraceLevel;    //TraceLevel.(TraceLevel)Enum.Parse(typeof(TraceLevel), it.Value);
            }
            catch (Exception)
            {
                throw new ArgumentException("Invalid trace level option. (Error, Warning, Info, Verbose, Off)");
            }

            //-o C:\Projekty\C##\E360.XML
            settings.outputFile = sOutFile;
        }

        // nastaví parametry komunikačního portu
        public static void GetPort(Settings settings, int iPort, int iBaudRate, int iDataBits, Parity oParity, StopBits oStopBits)
        {
            GXSerial serial;
            bool modeEDefaultValues = true;

            //settings.media = new GXSerial();
            serial = settings.media as GXSerial;
            // tmp = it.Value.Split(':');
            serial.PortName = "COM" + iPort.ToString();

            if (true) //tmp.Length > 1)
            {
                modeEDefaultValues = false;
                serial.BaudRate = iBaudRate;
                serial.DataBits = iDataBits;
                serial.Parity = oParity;
                serial.StopBits = oStopBits;
            }
            //else
            //{
            //    if (settings.client.InterfaceType == InterfaceType.HdlcWithModeE)
            //    {
            //        serial.BaudRate = 300;
            //        serial.DataBits = 7;
            //        serial.Parity = Parity.Even;
            //        serial.StopBits = StopBits.One;
            //    }
            //    else
            //    {
            //        serial.BaudRate = 9600;
            //        serial.DataBits = 8;
            //        serial.Parity = Parity.None;
            //        serial.StopBits = StopBits.One;
            //    }
            //}
        }

        // nastaví jméno a atribut požaadovaného objektu
        public static void GetSelectedObject(Settings settings, string sName, int iAtt)
        {
            if (settings.readObjects.Count > 0)
                settings.readObjects.Clear();

            settings.readObjects.Add(new KeyValuePair<string, int>(sName, iAtt));
        }

    }
}
