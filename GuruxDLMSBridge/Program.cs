//
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
using Gurux.Serial;
using Gurux.Net;
using Gurux.DLMS.Enums;
using System.Threading;
using Gurux.DLMS.Objects;
using Gurux.MQTT;
using System.Diagnostics;
using System.Security.Policy;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.IO.Ports;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Linq;
using Gurux.Common;
using System.Data.Common;
using System.Data;
using System.Xml.Linq;

namespace Gurux.DLMS.Client.Example
{
    class Program
    {
        // 1. Definition of Structure
        public struct DLMSBridgeSetting
        {
            public int iSerialNumber;
            public string sOutfile;
            //public int ThreadId;
            //public ushort clientNo;
            public Dictionary<string, object> config;
        }

        // 2. Thread-safe kolekce pro uložení dat z vláken
       // private static ConcurrentBag<DLMSBridgeSetting> threadDataCollection = new ConcurrentBag<DLMSBridgeSetting>();

        // 3. ThreadLocal pro izolovanou instanci MyData
        //private static ThreadLocal<DLMSBridgeSetting> localData = new ThreadLocal<DLMSBridgeSetting>(() => new DLMSBridgeSetting());



        //public const byte NO_ERROR = 0;
        //public const byte NO_ASSOCIATION = 1;
        //public const byte OBIS_NOT_EXIST = 2;
        //public const byte NO_SAMPLE_IN_INTERVAL = 3;
        //public const byte BAD_COMMAND_SYNTAX = 4;
        //public const byte NO_OBJ_ATTR = 4;
        //public const byte UKNOWN_REASON = 255;

        static int bufferSize;
        static ushort listenPort;
        static ushort connCounter = 0;

        //First - with detailed DLMS library logs
        // prvni - s podrobnymi vypisy DLMS knihovny
        static ushort SledovanaKomunikace = 0x8001; 

        unsafe static byte DoubleToByte(double Num, int pos)
        {//vraci byte v IEEE strukture
            byte* Pointer = (byte*)&Num;
            if (pos > 7) return 0;   //chyba
            else return Pointer[pos];
        }

        private static double ConvertObjectValueToDouble(object val)
        {
            double dVal = 0;

            if (val == null)
            {
                throw new Exception("Hodnota val je null");
            }
            else if (val is byte)
            {
                dVal = Convert.ToDouble(val);
            }
            else if (val is byte[])
            {
                dVal = Convert.ToDouble(GXCommon.ToHex((byte[])val, true));
            }
            else if (val is double || val is int || val is UInt16 || val is UInt32 || val is UInt64)
            {
                dVal = Convert.ToDouble(val);
            }
            else if (val is string)
            {
                dVal = Convert.ToDouble(val);
            }
            else
            {
                Type type = val.GetType();
                Console.WriteLine($"Typ: {type.FullName}");

                foreach (var property in type.GetProperties())
                {
                    Console.WriteLine($"Vlastnost: {property.Name}, Typ: {property.PropertyType}");
                }

                throw new Exception("Hodnota je neznámý typ");
            }


            return dVal;
        }

        [DllImport("kernel32")] private static extern ushort GetPrivateProfileInt(String Section, String Key, int Default, String FilePath);
        [STAThread]
        static void Main()
        {
            IPAddress listenInterface = IPAddress.Any;

            string FullPath = Directory.GetCurrentDirectory();
            FullPath += "\\DLMSAisys.INI";

            listenPort = GetPrivateProfileInt("MAIN", "ListenPort", 5000, FullPath);
            bufferSize = GetPrivateProfileInt("MAIN", "BufferSize", 4096, FullPath);

            TcpListener tcpServer = null;
            TcpClient tcpClient = null;

            //TCP spojení
            // TCP connection
            try
            {
                // Create the TCP server
                FTisk(0, "TCP Listener: Creating the TCP server...\n");
                tcpServer = new TcpListener(listenInterface, (int)listenPort);
                FTisk(0, "TCP Listener: TcpListener created on address {0} and port {1}\n",
                    listenInterface.ToString(),
                    listenPort
                    );

                // Start listening for connections
                FTisk(0, "TCP Listener: Start listening for connections...\n");
                tcpServer.Start();

                // Wait for a client connection
                FTisk(0, "TCP Listener: Waiting for a client connection...\n");

                while (true)
                {
                    try
                    {
                        SledovanaKomunikace = PrikazyKlavesnice(SledovanaKomunikace);
                        Thread.Sleep(100);
                        // If there is no request, do not process.
                        //pokud neni pozadavek - nezpracuji
                        if (tcpServer.Pending())
                        {
                            tcpClient = tcpServer.AcceptTcpClient();
                            ThreadPool.QueueUserWorkItem(ThreadProc, tcpClient);
                        }
                    }
                    finally { }
                }
            }
            catch (SocketException err)
            {
                // Exceptions on the TcpListener are caught here
                FTisk(0, "TCP Listener: Socket error occurred: {0}\n", err.Message);
            }
            catch (System.IO.IOException err)
            {
                // Exceptions on the NetworkStream are caught here
                FTisk(0, "TCP Listener: I/O error: {0}\n", err.Message);
            }
            finally
            {
                // Close any remaining open resources
                if (tcpServer != null)
                    tcpServer.Stop();
                if (tcpClient != null)
                    tcpClient.Close();
            }

        }


        private unsafe static void ThreadProc(object obj)
        {
            DLMSBridgeSetting stBridgeSetting = new DLMSBridgeSetting();

            string sStrMsg;
            //každý thread má své èíslo pro identifikaci výpisù
            // Each thread has its own number for identifying logs.
            ushort clientNo = ++connCounter;   
            NetworkStream tcpStream = null;

            int Inx;
            int minLM;
            int Len;
            int PosIn = 0;
            int nBytes;
            byte[] receiveBuffer = new byte[bufferSize];
            byte[] InMessage = new byte[bufferSize];
            byte[] OutMessage = new byte[bufferSize];

            //int nBytes;
            bool mStatus = false;    //disconected
            var client = (TcpClient)obj;
            FTisk(clientNo, "*TCP Listener: Connection accepted\n");

            Settings settings = new Settings();

            Reader.GXDLMSReader reader = null;

            // default setting
            // Handle command line parameters.
            // metoda nastaví default parametry, do budoucna se bude využívat jen pro testovací úèely, plnì bude nahrazena metodami níže
            // The method sets default parameters; in the future, it will be used for testing purposes only and will be fully replaced by the methods below.
            int ret = Settings.GetParameters(Constants.ARGS, settings);
            if (ret != 0)
            {
                return;
            }

            while (true)
            {
                byte Err = Constants.UKNOWN_REASON;   //not set

                try
                {
                    if (client.Connected)
                    {
                        tcpStream = client.GetStream();
                        if (tcpStream == null)
                        {
                            FTisk(clientNo, "*Stream uz neexistuje - uklidim\n");
                            break;
                        }
                    }
                    else
                    {
                        FTisk(clientNo, "*Ze streamu nelze cist - uklidim\n");
                        break;
                    }

                    nBytes = tcpStream.Read(receiveBuffer, 0, bufferSize);
                    //po rozpojeni spojeni to ukoncim
                    // After disconnecting the connection, terminate it.
                    if (nBytes < 1) break; 

                    if (PosIn + nBytes < bufferSize)
                    {
                        Buffer.BlockCopy(receiveBuffer, 0, InMessage, PosIn, nBytes);
                        PosIn += nBytes;
                    }
                    // minimální délka
                    // Minimum length
                    if (PosIn >= 5)
                    {
                        uint Command = ((uint)InMessage[2] << 8) + InMessage[1];
                        uint DelkaZeZpravy = ((uint)InMessage[4] << 8) + InMessage[3];
                        //pripocte i uvodni BYTE Pocket + WORD Kod + WORD length
                        // Adds the initial BYTE Pocket + WORD Code + WORD Length.
                        DelkaZeZpravy += 5;   
                        byte PacketNo = InMessage[0];
                   
                        FTisk(clientNo, "*-->");
                        for (int ii = 0; ii < PosIn; ii++)
                            FTisk(clientNo, "{0} ", InMessage[ii].ToString("X2"));
                        FTisk(clientNo, "\n");

                        //sedi zakladni zabezpeceni
                        // Basic security matches.
                        if (DelkaZeZpravy == PosIn)
                        {
                            //Priprava paketu pro odeslani
                            OutMessage[0] = PacketNo;               // Responds with the same packet number. //odpovida na stejne cislo paketu
                            OutMessage[1] = (byte)(Command & 0xFF);                 //<word> default responce (command + 0x1000)
                            OutMessage[2] = (byte)(((Command >> 8) + 0x10) & 0xFF);
                            Len = 0;

                            switch (Command)
                            {

                                case 0x00FF://test

                                    //MainGuruxMeth(Constants.ARGS);

                                    //ret = Settings.GetParameters(Constants.ARGS, settings);
                                    //if (ret != 0)
                                    //{
                                    //    return;
                                    //}

                                    Len = 1;
                                    if (mStatus) OutMessage[5] = 0;  //DLMS assoc. establish
                                    else OutMessage[5] = 1;  //DLMS assoc. not establish
                                    break;

                                case 0x0000: //Read Status
                                    Len = 1;
                                    if (mStatus) OutMessage[5] = 0;  //DLMS assoc. establish
                                    else OutMessage[5] = 1;  //DLMS assoc. not establish

                                    break;

                                case 0x0001: //Open Channel 01 0100 2200 14 "COM6,9600,8N1,LGZ72980038" 00 
                                    try
                                    {
                                        int COMport = 0, speed = 300, bits = 7;
                                        // int swIEC = 1;
                                        // int swSNuse = 0;
                                        // ushort sAdr = 1, cAdr = 4;
                                        int iSecurityLevel;
                                        string vc = "";
                                        System.IO.Ports.Parity parity = System.IO.Ports.Parity.None;

                                        //marpi - zavøu reader, on odpojí porty a zlikviduje nìkteré objekty - nastavit na null
                                        // Marpi - close the reader, it will disconnect ports and dispose of some objects - set to null.
                                        if (mStatus) reader?.Close();  
                                        reader = null;

                                        mStatus = false;

                                        Err = Constants.BAD_COMMAND_SYNTAX;    // Invalid string - syntax as default.
                                        //01 0100 2200 14 "COM6,9600,8N1,LGZ72980038" 00
                                        minLM = 6; // Minimum message length: 1 + 2 + 2 + 1 + strlen.
                                        if (DelkaZeZpravy > minLM)
                                        {
                                            // Decoding the incoming string.
                                            int startIndex = 6;
                                            int endIndex = Array.IndexOf(InMessage, (byte)0x00, startIndex);

                                            int length = endIndex - startIndex;
                                            string decodedString = Encoding.ASCII.GetString(InMessage, startIndex, length);

                                            FTisk(clientNo, "decodedString: {0}: \n", decodedString);

                                            // Rozdìlit øetìzec podle èárky
                                            // Split the string by a comma.
                                            string[] parsers = decodedString.Split(',');

                                            // èíslo 14 hexa je 20 odpovídá záznamu v AuthenticationTypes {"20","Low", "Low"}, které definuje blok v ini souboru
                                            // The number 14 in hex is 20, corresponding to the entry in AuthenticationTypes {"20", "Low", "Low"}, which defines the block in the INI file.
                                            // [Low]
                                            //SecurityLevel = "Low"
                                            iSecurityLevel = (int) InMessage[5];

                                            // port
                                            if (InMessage[6] == 'C' && InMessage[7] == 'O' && InMessage[8] == 'M')
                                            {// Processing the opening via COM here.

                                                //"COM6,9600,8N1,LGZ72980038"
                                                Inx = 0;
                                                foreach (string par in parsers)
                                                {
                                                    switch (Inx++)
                                                    {
                                                        case 0:                                                           
                                                            int.TryParse(par.Substring(3), out COMport);
                                                            break;
                                                       
                                                        case 1:
                                                            int.TryParse(par, out speed);
                                                            break;
                                                        case 2:
                                                            if (par[0] == '7' || par[0] == '8') bits = (int) (par[0] - '0');

                                                            switch (par[1])
                                                            {
                                                                case 'E':
                                                                    parity = System.IO.Ports.Parity.Even;
                                                                    break;
                                                                case 'O':
                                                                    parity = System.IO.Ports.Parity.Odd;
                                                                    break;
                                                                case 'N':
                                                                    parity = System.IO.Ports.Parity.None;
                                                                    break;
                                                            }
                                                            break;
                                                        case 3:
                                                            vc = par;
                                                            break;
                                                        
                                                    }
                                                }

                                                if (Inx >= 4)
                                                {// 4 parameters separated by commas.
                                                    Err = Constants.NO_ASSOCIATION;   // If it does not open, set 1 = AssociationNotCreated.
                                                    stBridgeSetting.sOutfile = "no file";
                                                    //naètení informaci o mìøidle ze souboru
                                                    // Loading meter information from the file.
                                                    if (ReadStationFile(iSecurityLevel, vc, ref stBridgeSetting, out string sReason)<0)
                                                        FTisk(clientNo, "ReadStationFile: {0}: \n", sReason);
                                                    else
                                                    {
                                                        FTisk(clientNo, "iSerialNumber: {0}: \n", stBridgeSetting.iSerialNumber);
                                                        FTisk(clientNo, "ClientAddress: {0}: \n", stBridgeSetting.config["ClientAddress"]);
                                                        FTisk(clientNo, "SecurityLevel: {0}: \n", stBridgeSetting.config["SecurityLevel"]);
                                                        FTisk(clientNo, "Password: {0}: \n", stBridgeSetting.config["Password"]);
                                                    }

                                                    //!!!!! ošetøi výjimky, které mùžou vyhodit metody GetAccessInfo, GetCommonSetting
                                                    // !!!!! Handle exceptions that may be thrown by the methods GetAccessInfo and GetCommonSetting.

                                                    // nastavení pøístupových informací- adresa serveru, adresa klienta, úroveò zabezpeèení, heslo
                                                    // Setting access information - server address, client address, security level, password.
                                                    //Settings.GetAccessInfo(settings, 72980038, 32, "Low", "00000000");
                                                    SettingsAise.GetAccessInfo(settings, stBridgeSetting.iSerialNumber, (int) stBridgeSetting.config["ClientAddress"], stBridgeSetting.config["SecurityLevel"].ToString(), stBridgeSetting.config["Password"].ToString());

                                                    //Settings.GetCommonSetting(settings, (byte)1, TraceLevel.Verbose, "C:\\Projekty\\C##\\LGZ72980038.XML");
                                                    SettingsAise.GetCommonSetting(settings, (byte)1, TraceLevel.Verbose, stBridgeSetting.sOutfile.ToString());

                                                    // Setting up communication.
                                                    SettingsAise.GetPort(settings, COMport, speed, bits, parity, StopBits.One);

                                                    FTisk(clientNo, "*Open:COM{0}:{1}:{2}{3}{4}\n", COMport, speed, bits, parity, StopBits.One);
                                                  
                                                    //Initialize connection settings.
                                                    if (settings.media is GXSerial)
                                                    {
                                                    }
                                                    else if (settings.media is GXNet)
                                                    {
                                                    }
                                                    else if (settings.media is GXMqtt)
                                                    {
                                                    }
                                                    else
                                                    {
                                                        throw new Exception("Unknown media type.");
                                                    }

                                                    // pokud je reader null a mìl by tady vždy být, tak ho vytvoøím a nastavím podle settings
                                                    // If the reader is null (which it should be), create and configure it according to the settings.
                                                    if (reader == null)
                                                        reader = new Reader.GXDLMSReader(settings.client, settings.media, settings.trace, settings.invocationCounter);


                                                    reader.OnNotification += (data) =>
                                                    {
                                                        Console.WriteLine(data);
                                                    };

                                                    //Create manufacturer spesific custom COSEM object.
                                                    settings.client.OnCustomObject += (type, version) =>
                                                    {
                                                        /*
                                                        if (type == 6001 && version == 0)
                                                        {
                                                            return new ManufacturerSpesificObject();
                                                        }
                                                        */
                                                        return null;
                                                    };

                                                    try
                                                    {
                                                        settings.media.Open();

                                                    }
                                                    catch (System.IO.IOException ex)
                                                    {
                                                        Console.WriteLine("----------------------------------------------------------");
                                                        Console.WriteLine(ex.Message);
                                                        Console.WriteLine("Available ports:");
                                                        Console.WriteLine(string.Join(" ", GXSerial.GetPortNames()));
                                                        throw new Exception("Open media failed ", ex);                                                      
                                                    }

                                                    // nìkterá mìøidla vyžadují chvilku poèkat
                                                    // Some meters require a short wait.
                                                    Thread.Sleep(1000);
                                                    Console.WriteLine("Connected:");

                                                    if (settings.media is GXNet net && settings.client.InterfaceType == InterfaceType.CoAP)
                                                    {
                                                        //Update token ID.
                                                        settings.client.Coap.Token = 0x45;
                                                        settings.client.Coap.Host = net.HostName;
                                                        settings.client.Coap.MessageId = 1;
                                                        settings.client.Coap.Port = (UInt16)net.Port;
                                                        //DLMS version.
                                                        settings.client.Coap.Options[65001] = (byte)1;
                                                        //Client SAP.
                                                        settings.client.Coap.Options[65003] = (byte)settings.client.ClientAddress;
                                                        //Server SAP
                                                        settings.client.Coap.Options[65005] = (byte)settings.client.ServerAddress;
                                                    }

                                                    reader.InitializeConnection();


                                                    if (reader.GetAssociationView(settings.outputFile))
                                                    {

                                                        reader.GetScalersAndUnits();
                                                        reader.GetProfileGenericColumns();
                                                      
                                                    }
                                                    FTisk(clientNo, "Asociace OK: COM{0}:{1}:{2}{3}{4}\n", COMport, speed, bits, parity, StopBits.One);

                                                    mStatus = true;

                                                }
                                            }
                                            else
                                            {
                                                //zde se bude zpracovavat otevreni pres TCP pipe
                                                // Processing the opening via TCP pipe here.
                                                //if (!mStatus)
                                                //{
                                                //}
                                            }

                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        FTisk(clientNo, "*Open error {0}\n", ex.Message);
                                        reader?.Close();
                                        mStatus = false;
                                        PosIn = 0;
                                    }
                                   
                                    if (!mStatus)
                                        reader?.Close();

                                    if (mStatus)
                                    {
                                        OutMessage[5] = 0;  //connected
                                        FTisk(clientNo, "Kanal otevren:\n");
                                    }
                                    else
                                    {
                                        OutMessage[5] = Err;  //diconected
                                        FTisk(clientNo, "Kanal se nepodarilo otevrit:\n");
                                    }

                                    Len = 1;
                                    
                                    break;

                                case 0x0002: //Read attribute <byte>CP <word>2 <word>length <word>class <word>attribute <string>longname
                                    try
                                    {
                                        minLM = 9;  // Minimum message length: 1+2+2+2+2+strlen
                                        if (DelkaZeZpravy > minLM)
                                        {
                                            // If there is no association, exit.
                                            if (!mStatus)
                                            {
                                                Err = Constants.NO_ASSOCIATION;   // It looks like the association is not created.
                                                throw new Exception("UzivEx: No Association");     
                                            }
                                            uint cla = ((uint)InMessage[6] << 8) + InMessage[5];
                                            uint att = ((uint)InMessage[8] << 8) + InMessage[7];

                                            for (Inx = minLM; Inx < PosIn; Inx++) if (InMessage[Inx] == 0) break;

                                            //reader.TargetObisCode = Encoding.UTF8.GetString(InMessage, minLM, Inx - minLM);
                                            string sTargetObisCode = Encoding.UTF8.GetString(InMessage, minLM, Inx - minLM);

                                            FTisk(clientNo, "*ONREAD ID:{0},CLASS:{1},ATTR:{2}\n", sTargetObisCode,
                                                cla,    //ClassIDstring((ushort)cla),
                                                att
                                                );

                                            Err = Constants.OBIS_NOT_EXIST; // If there is an error, it does not have OBIS.

                                            // Select the object based on OBIS.
                                            SettingsAise.GetSelectedObject(settings, sTargetObisCode, (int)att);

                                            object val = null;

                                            foreach (KeyValuePair<string, int> it in settings.readObjects)
                                            {
                                                // Select the object matching the LN and read it based on the attribute.
                                                //object val = reader.Read(settings.client.Objects.FindByLN(ObjectType.None, "1.0.72.7.255"), 2);

                                                val = reader.Read(settings.client.Objects.FindByLN(ObjectType.None, it.Key), it.Value);

                                                // Display the value on the console.
                                                reader.ShowValue(val, it.Value);
                                            }

                                            Len = 1;
                                            if (mStatus) OutMessage[5] = 0;  //DLMS assoc. establish
                                            else OutMessage[5] = 1;  //DLMS assoc. not establish

                                            // Convert the value val to double and then convert it into individual bytes.
                                            try
                                            {
                                                double dVal=0;

                                                dVal = ConvertObjectValueToDouble(val);

                                                OutMessage[5] = Constants.TYP_DOUBLE;  
                                                OutMessage[6] = DoubleToByte(dVal, 0);  
                                                OutMessage[7] = DoubleToByte(dVal, 1);
                                                OutMessage[8] = DoubleToByte(dVal, 2);
                                                OutMessage[9] = DoubleToByte(dVal, 3);
                                                OutMessage[10] = DoubleToByte(dVal, 4);
                                                OutMessage[11] = DoubleToByte(dVal, 5);
                                                OutMessage[12] = DoubleToByte(dVal, 6);
                                                OutMessage[13] = DoubleToByte(dVal, 7);
                                                Len = 1 + sizeof(double);
                                            }
                                            catch (Exception ex)
                                            {
                                                throw new Exception("Konverze na double se nezdaøila - "+ex.Message);
                                            }
                                        }
                                    }
                                    catch (Exception Ex)
                                    {

                                        OutMessage[1] = (byte)(Command & 0xFF);
                                        OutMessage[2] = (byte)(((Command >> 8) + 0x20) & 0xFF);
                                        OutMessage[5] = Err;   //code error
                                        Len = 1;

                                        FTisk(clientNo, "*Read: {0}\n", Ex.Message);
                                        PosIn = 0;
                                    }

                                    break;

                                case 0x0003: //Read time/date <byte>CP <word>3 <word>length <string>longname
                                    try
                                    {
                                        minLM = 5;  // Minimum message length: 1+2+2+strlen

                                        if (DelkaZeZpravy >= minLM)
                                        {
                                            // If there is no association, exit.
                                            if (!mStatus)
                                            {
                                                Err = Constants.NO_ASSOCIATION;                 // It looks like the association is not created.
                                                throw new Exception("UzivEx: No Association");  
                                            }

                                            int attClock = 2;
                                            int attDST = 8;

                                            Err = Constants.OBIS_NOT_EXIST; // If there is an error, it does not have OBIS.

                                            object valClock = null;
                                            object valDST = null;

                                            bool bLZTime = false;

                                            // Reading from the INI file.
                                            string sObisClock = stBridgeSetting.config["ClockObis"].ToString();

                                            FTisk(clientNo, "*ONREAD ID:{0},ATTR:{1}\n", sObisClock,

                                               attClock
                                               );

                                            // Select the object based on OBIS.
                                            SettingsAise.GetSelectedObject(settings, sObisClock, attClock);

                                            Err = Constants.NO_OBJ_ATTR;  // If there is an error, it does not contain the attribute.

                                            // read the time
                                            foreach (KeyValuePair<string, int> it in settings.readObjects)
                                            {
                                                // Select the object matching the LN and read it based on the attribute.
                                               
                                                valClock = reader.Read(settings.client.Objects.FindByLN(ObjectType.None, it.Key), it.Value);

                                                // Display the value on the console.
                                                reader.ShowValue(valClock, it.Value);

                                            }

                                            // Select the object based on OBIS.
                                            SettingsAise.GetSelectedObject(settings, sObisClock, attDST);

                                            // Read DST (Daylight Saving Time).
                                            foreach (KeyValuePair<string, int> it in settings.readObjects)
                                            {
                                                // Select the object matching the LN and read it based on the attribute.

                                                valDST = reader.Read(settings.client.Objects.FindByLN(ObjectType.None, it.Key), it.Value);

                                                // Display the value on the console.
                                                reader.ShowValue(valDST, it.Value);

                                            }

                                            if(valDST != null)
                                                bLZTime = (bool)valDST;

                                            Len = 1;
                                            if (mStatus) OutMessage[5] = 0;  //DLMS assoc. establish
                                            else OutMessage[5] = 1;  //DLMS assoc. not establish

                                            if (valClock is GXDateTime dateTime)
                                            {
                                                DateTimeOffset meterTimeOffset = dateTime.Value;
                                                DateTime meterTime = meterTimeOffset.LocalDateTime;

                                                OutMessage[5] = Constants.TYP_TIME;  //typ string
                                                OutMessage[6] = (byte)meterTime.Hour;
                                                OutMessage[7] = (byte)meterTime.Minute;
                                                OutMessage[8] = (byte)meterTime.Second;
                                                OutMessage[9] = (byte)meterTime.Day;
                                                OutMessage[10] = (byte)meterTime.Month;
                                                OutMessage[11] = (byte)(meterTime.Year & 0xFF);

                                                if (bLZTime)
                                                    OutMessage[12] = (byte)(((meterTime.Year >> 8) + 0x80) & 0xFF); // Year + DST enabled flag.
                                                else
                                                    OutMessage[12] = (byte)((meterTime.Year >> 8) & 0xFF);

                                                Len = 1 + 7;

                                            }

                                        }
                                    }
                                    catch (Exception Ex)
                                    {
                                        OutMessage[1] = (byte)(Command & 0xFF);
                                        OutMessage[2] = (byte)(((Command >> 8) + 0x20) & 0xFF);
                                        OutMessage[5] = Err;   //kod error
                                        Len = 1;

                                        FTisk(clientNo, "*Time: {0}\n", Ex.Message);
                                        PosIn = 0;
                                    }
                                    break;

                                case 0x0004: //Read profile <byte>CP <word>4 <word>length <OD:typtime>19 45 06 22 4 2023 <DO:typtime>00 00 00 23 4 2023  <string>longname(profile obis)
                                    try
                                    {
                                        minLM = 19;  // Minimum message length: 1+2+2+7+7+strlen
                                        if (DelkaZeZpravy > minLM)
                                        {
                                            // If there is no association, exit.
                                            if (!mStatus)
                                            {
                                                Err = Constants.NO_ASSOCIATION;                    //vypada ze neni vytvorena associace
                                                throw new Exception("UzivEx: No Association");     
                                            }

                                            ushort yr;
                                            yr = InMessage[11]; yr <<= 8; yr += InMessage[10];
                                            yr &= 0x7FFF;   // Sends the DST flag in the highest bit.

                                            // poèátek a konec období
                                            // !!!!! MARPI bude potøeba asi udìlat TimeZoneInfo.Local, ale teï to testujeme i takto
                                            // Start and end of the period
                                            // !!!!! MARPI: It might be necessary to use TimeZoneInfo.Local, but for now, we are testing it this way.
                                            DateTime start = new DateTime(yr, InMessage[9], InMessage[8], InMessage[5], InMessage[6], InMessage[7]);
                                            DateTime end = new DateTime(yr, InMessage[16], InMessage[15], InMessage[12], InMessage[13], InMessage[14]);

                                            FTisk(clientNo, "Filtr: Od {0} Do {1}\n", start.ToString(), end.ToString());

                                            for (Inx = minLM; Inx < PosIn; Inx++) if (InMessage[Inx] == 0) break;   // Find the end of the string (OBIS profile).

                                            string sObis = Encoding.UTF8.GetString(InMessage, minLM, Inx - minLM);

                                            // Select the object based on OBIS.
                                            GXDLMSObject profileObject = settings.client.Objects.FindByLN(ObjectType.ProfileGeneric, sObis);

                                            if (profileObject == null)
                                            {
                                                Console.WriteLine("Profil s daným OBIS kódem nebyl nalezen.");
                                                throw new Exception("Nemám žádný profil");
                                            }
                                            else
                                            {
                                                Console.WriteLine($"Profil s {sObis} kódem byl nalezen - {profileObject.Description}");

                                            }

                                            Console.WriteLine("Start read profileGeneric");

                                            // <CaptureObject> v XML
                                            var captureObjects = ((GXDLMSProfileGeneric)profileObject).CaptureObjects;

                                            // Check if there are any <CaptureObject> in the XML.
                                            if (captureObjects != null && captureObjects.Count > 0)
                                            {
                                                Console.WriteLine("Seznam XML Capture Objects:");

                                                foreach (var obj1 in captureObjects)
                                                {
                                                    GXDLMSObject dlmsObject = obj1.Key;

                                                    GXDLMSCaptureObject captureObjectOne = obj1.Value;

                                                    // Attribute Index
                                                    int attributeIndex = captureObjectOne.AttributeIndex;

                                                    Console.WriteLine($"\n*OBIS: {obj1.Key} *Object Type: {dlmsObject.ObjectType} *Attribute Index: {attributeIndex} *");
                                                    Console.WriteLine($"Logical Name (LN): {dlmsObject.LogicalName}");
                                                    Console.WriteLine($"Short Name (SN): {dlmsObject.ShortName}");
                                                    Console.WriteLine($"Object Type: {dlmsObject.ObjectType}");
                                                    Console.WriteLine($"Description: {dlmsObject.Description}");
                                                    Console.WriteLine($"Name: {dlmsObject.Name}");
                                                    Console.WriteLine($"Version: {dlmsObject.Version}");
                                                }

                                            }
                                            else // If they are not in the XML, attempt to read them from the meter.
                                            {
                                                Console.WriteLine("XML Capture Objects nejsou k dispozici.");

                                                // ètení seznamu capture objects POZOR parametr 3 (2 ète všechna data desítky minut)
                                                // Reading the list of capture objects.
                                                // WARNING: Parameter 3 (2 reads all data, taking tens of minutes).
                                                reader.Read(profileObject, 3); // Read Capture Objects

                                                // if any
                                                if (captureObjects != null && captureObjects.Count > 0)
                                                {
                                                    Console.WriteLine("Seznam Capture Objects naèten pøímým ètením:");
                                                    foreach (var obj1 in captureObjects)
                                                    {
                                                        GXDLMSObject dlmsObject = obj1.Key;

                                                        GXDLMSCaptureObject captureObjectOne = obj1.Value;

                                                        // Attribute Index
                                                        int attributeIndex = captureObjectOne.AttributeIndex;

                                                        Console.WriteLine($"\n*OBIS: {obj1.Key} *Object Type: {dlmsObject.ObjectType} *Attribute Index: {attributeIndex} *");

                                                        Console.WriteLine($"Logical Name (LN): {dlmsObject.LogicalName}");
                                                        Console.WriteLine($"Short Name (SN): {dlmsObject.ShortName}");
                                                        Console.WriteLine($"Object Type: {dlmsObject.ObjectType}");
                                                        Console.WriteLine($"Description: {dlmsObject.Description}");
                                                        Console.WriteLine($"Name: {dlmsObject.Name}");
                                                        Console.WriteLine($"Version: {dlmsObject.Version}");
                                                    }

                                                }
                                                else
                                                {
                                                    Console.WriteLine("Capture Objects nebyly naèteny pøímým ètením.");
                                                }

                                            }

                                            Console.WriteLine("Konec read profileGeneric");

                                            // Vytvoøení požadavku na ètení s èasovým filtrem
                                            // Creating a read request with a time filter.
                                            GXDateTime startgx = new GXDateTime(start);
                                            GXDateTime endgx = new GXDateTime(end);

                                            object[] rowData = null;                                         
                                            int iNumObjects = 0;

                                            // I have captureObjects, reading data from startgx to endgx.
                                            if (captureObjects != null && captureObjects.Count > 0)
                                            {
                                                var data = reader.ReadRowsByRange((GXDLMSProfileGeneric)profileObject, startgx, endgx);

                                                iNumObjects = data.Count();
                                                //fiNumRowsData = new int[iNumObjects];

                                                Inx = 5;    // First position in OutBuffer for data.
                                                OutMessage[Inx++] = (byte)(iNumObjects & 0xFF);
                                                OutMessage[Inx++] = (byte)((iNumObjects >> 8) & 0xFF);

                                                FTisk(clientNo, "pocetobjektu- {0} pocetobjektu>>8 -{1}\n", OutMessage[Inx - 2], OutMessage[Inx - 1]);

                                                double dVal = 0;

                                                foreach (var row in data)
                                                {
                                                    int i = 0;
                                                    // Each row is an array of objects corresponding to the columns.
                                                    rowData = (object[])row;

                                                    // Display each column value in the row.
                                                    Console.WriteLine("Row:");
                                                    OutMessage[Inx++] = (byte) rowData.Length;
                                                    FTisk(clientNo, "pocet radku-{0}\n", OutMessage[Inx - 1]);

                                                    for (int j = 0; j < rowData.Length; j++)
                                                    {
                                                       
                                                        if (j == 0) // date and time
                                                        {
                                                            if(!DateTime.TryParse(rowData[j].ToString(), out DateTime dtRow))
                                                                Console.WriteLine($"Neplatný formát èasové znaèky {rowData[j].ToString()} ");


                                                            OutMessage[Inx++] = Convert.ToByte(dtRow.Hour);
                                                            OutMessage[Inx++] = Convert.ToByte(dtRow.Minute);
                                                            OutMessage[Inx++] = Convert.ToByte(dtRow.Second);

                                                            FTisk(clientNo, "hodina-{0} minuta{1} sekunda{2}", OutMessage[Inx - 3], OutMessage[Inx - 2], OutMessage[Inx - 1]);

                                                            OutMessage[Inx++] = Convert.ToByte(dtRow.Day);
                                                            OutMessage[Inx++] = Convert.ToByte(dtRow.Month);

                                                            FTisk(clientNo, "den-{0} mesic{1}", OutMessage[Inx - 2], OutMessage[Inx - 1]);


                                                            OutMessage[Inx++] = (byte)(Convert.ToByte(dtRow.Year & 0xFF) );
                                                            OutMessage[Inx++] = (byte)((Convert.ToByte(dtRow.Year >> 8) & 0xFF));

                                                            FTisk(clientNo, " Rok-{0}{1}", OutMessage[Inx-2], OutMessage[Inx-1]);
                                                        }
                                                        else // value
                                                        {
                                                            dVal = ConvertObjectValueToDouble(rowData[j]);
                                                            FTisk(clientNo, "hodnota{0} \n", dVal);
                                                            OutMessage[Inx++] = Constants.TYP_DOUBLE;  //type double
                                                            OutMessage[Inx++] = DoubleToByte(dVal, 0);  //konversio to byte
                                                            OutMessage[Inx++] = DoubleToByte(dVal, 1);
                                                            OutMessage[Inx++] = DoubleToByte(dVal, 2);
                                                            OutMessage[Inx++] = DoubleToByte(dVal, 3);
                                                            OutMessage[Inx++] = DoubleToByte(dVal, 4);
                                                            OutMessage[Inx++] = DoubleToByte(dVal, 5);
                                                            OutMessage[Inx++] = DoubleToByte(dVal, 6);
                                                            OutMessage[Inx++] = DoubleToByte(dVal, 7);
                                                        }

                                                    }
                                      
                                                    i++;
                                                }

                                            }

                                            Console.WriteLine($"Konec read ReadRowsByRange: {Inx}");
                                            
                                            Len = Inx - 6;

                                        }
                                    }
                                    catch (Exception Ex)
                                    {
                                        OutMessage[1] = (byte)(Command & 0xFF);
                                        OutMessage[2] = (byte)(((Command >> 8) + 0x20) & 0xFF);
                                        OutMessage[5] = Err;   //kod error
                                        Len = 1;

                                        FTisk(clientNo, "*Read profiles error {0}\n", Ex.Message);
                                        //reader.Close();
                                        //mStatus = false;
                                        PosIn = 0;
                                    }
                                    break;


                                case 0x0005:   //Close chanel
                                    try
                                    {
                                        mStatus = false;
                                        OutMessage[5] = 0;  //always OK
                                        Len = 1;
                                        FTisk(clientNo, "*Close channel\n");
                                        reader?.Close();     
                                    }
                                    catch (Exception Ex)
                                    {
                                        OutMessage[1] = (byte)(Command & 0xFF);
                                        OutMessage[2] = (byte)(((Command >> 8) + 0x20) & 0xFF);
                                        OutMessage[5] = Err;   //kod error
                                        Len = 1;

                                        FTisk(clientNo, "*Close error {0}\n", Ex.Message);
                                        //reader.Close();
                                        mStatus = false;
                                        PosIn = 0;
                                    }
                                    break;

                                case 0x0006:   // Delete xml file and new asociation
                                    try
                                    {
                                        // If there is no association, do not continue.
                                        if (!mStatus)
                                        {
                                            Err = Constants.NO_ASSOCIATION;                    // It looks like the association is not created.
                                            throw new Exception("UzivEx: No Association");     
                                        }


                                        mStatus = false;
                                        OutMessage[5] = 0;  
                                        Len = 1;
                                        FTisk(clientNo, "*Close channel\n");

                                        // If the reader exists, close it.
                                        reader?.Close();
                                        reader = null;

                                        // sOutfile must contain a string with the path and XML file name.
                                        if (!string.IsNullOrEmpty(stBridgeSetting.sOutfile))
                                        {
                                            // pokud soubor existuje, tak ho smažu
                                            if (File.Exists(stBridgeSetting.sOutfile))
                                            {
                                                File.Delete(stBridgeSetting.sOutfile);
                                                Console.WriteLine($"Soubor {stBridgeSetting.sOutfile} byl úspìšnì smazán.");
                                            }
                                            else
                                            {
                                                throw new Exception($"Zadána cesta {stBridgeSetting.sOutfile} není platná !");
                                            }
                                        }
                                        else
                                            throw new Exception("Není zadána cesta k XML souboru !");

                                        mStatus = false;
                                        OutMessage[5] = 0;  
                                        Len = 1;
                                        PosIn = 0;

                                    }
                                    catch (Exception Ex)
                                    {
                                        OutMessage[1] = (byte)(Command & 0xFF);
                                        OutMessage[2] = (byte)(((Command >> 8) + 0x20) & 0xFF);
                                        OutMessage[5] = Err;   //kod error
                                        Len = 1;

                                        FTisk(clientNo, "*Delete xml error {0}\n", Ex.Message);
                                        //reader.Close();
                                        mStatus = false;
                                        PosIn = 0;
                                    }
                                    break;

                                case 0x0010:
                                    try
                                    {
                                        DateTime datumcas = DateTime.Now;
                                        minLM = 12;  // Minimum message length: 1+2+2+1+1+1+1+1+1+2
                                        if (DelkaZeZpravy >= minLM)
                                        {
                                            if (!mStatus)
                                            {
                                                Err = Constants.NO_ASSOCIATION;                    // It looks like the association is not created.
                                                throw new Exception("UzivEx: No Association");   
                                            }

                                            ushort yr;
                                            yr = InMessage[12]; yr <<= 8; yr += InMessage[11];
                                            yr &= 0x7FFF;  // Sends the DST flag in the highest bit.

                                            string TypeTime = "";
                                            // Writes time in typtime format.
                                            if (InMessage[5] == 8)
                                            {
                                                TypeTime = "<typtime>";
                                            }

                                            datumcas = new DateTime(yr, InMessage[10], InMessage[9], InMessage[6], InMessage[7], InMessage[8]);

                                            FTisk(clientNo, "*Setting clock {6} Obis {0}-{1}-{2}-{3}-{4}-{5}\n", yr, InMessage[10], InMessage[9], InMessage[6], InMessage[7], InMessage[8], TypeTime);

                                        }

                                        string sObisClock = stBridgeSetting.config["ClockObis"].ToString();

                                        FTisk(clientNo, "*Setting clock Obis {0}\n", sObisClock);

                                        int attClock = 2;

                                        // Select the object based on OBIS.
                                        SettingsAise.GetSelectedObject(settings, sObisClock, attClock);

                                        KeyValuePair<string, int> it = settings.readObjects[0];

                                        // Najdeme objekt Clock podle logického názvu (LN)
                                        // Find the Clock object by logical name (LN).
                                        GXDLMSClock clock = (GXDLMSClock)settings.client.Objects.FindByLN(ObjectType.Clock, it.Key);

                                        // If the object exists, set the new time.
                                        if (clock != null)
                                        {

                                            // Get the local time zone.
                                            TimeZoneInfo localZone = TimeZoneInfo.Local;
                                            TimeSpan offset = localZone.GetUtcOffset(DateTime.Now);

                                            DateTimeOffset newTime =  new DateTimeOffset(datumcas, offset);

                                            clock.Time = new GXDateTime(newTime);

                                            // Write the value to the meter.
                                            reader.Write(clock, it.Value);

                                            FTisk(clientNo, "{0} Èas byl úspìšnì zapsán do mìøidla\n", datumcas);
                                            
                                        }
                                        else
                                        {
                                            FTisk(clientNo, "Objekt Clock nebyl nalezen.");
                                        }

                                    }
                                    catch(Exception Ex)
                                    {
                                        OutMessage[1] = (byte)(Command & 0xFF);
                                        OutMessage[2] = (byte)(((Command >> 8) + 0x20) & 0xFF);
                                        OutMessage[5] = Err;   //kod error
                                        Len = 1;

                                        FTisk(clientNo, "*Setting clock error {0}\n", Ex.Message);
                                        //reader.Close();
                                        mStatus = false;
                                        PosIn = 0;
                                    }

                                    break;

                                default:
                                    Err = Constants.UNIMPLEMETED_COMMAND;
                                    OutMessage[1] = (byte)(Command & 0xFF);
                                    OutMessage[2] = (byte)(((Command >> 8) + 0x20) & 0xFF);
                                    OutMessage[5] = Err;   //kod error
                                    Len = 1;

                                    PosIn = 0;
                                    break;
                            }

                            OutMessage[3] = (byte)(Len & 0xFF);             //<word>length
                            OutMessage[4] = (byte)((Len >> 8) & 0xFF);
                            FTisk(clientNo, "*<--");
                            for (int ii = 0; ii < Len + 5; ii++)
                                FTisk(clientNo, "{0} ", OutMessage[ii].ToString("X2"));
                            FTisk(clientNo, "\n");

                            tcpStream.Write(OutMessage, 0, Len + 5);
                        }
                        else
                        {
                            FTisk(clientNo, "*Nesedi delka (Body:{0}<>Incom:{1})\n", DelkaZeZpravy, PosIn - 5);
                        }
                        PosIn = 0;
                    }
                }
                catch (Exception ex)
                {
                    sStrMsg = ex.Message;
                    FTisk(clientNo, "*Error {0}\n", ex.Message);
                    reader?.Close();
                    mStatus = false;
                    PosIn = 0;
                }
            }
            reader?.Close();   
            mStatus = false;
            FTisk(clientNo, "*TCP Listener: Closing client tcp stream...\n");
            Thread.Sleep(2000); // To flush to TCP.
            tcpStream.Close();
            tcpStream = null;
            if (clientNo == connCounter) connCounter--;   //pokud jde o ukonceny posledni mohu ho vratit

        }

        // Constructs the full path based on the meter's serial number to the INI and XML files. Reads the corresponding password, address, etc., from the INI file according to the specified iSecurityLevel.
        // sestaví úplnou cestu podle sàiového èísla mìøidla k souboru ini a XML. Z ini souboru pøeète podle zadané iSecurityLevel odpovídající heslo, adresu atd.
        private static int ReadStationFile(int iSecurityLevel, string sSerialNumber, ref DLMSBridgeSetting stBridgeSetting, out string sReason)//out int numberPart)
        {
            sReason = "";
            
            if (string.IsNullOrEmpty(sSerialNumber))
            {
                stBridgeSetting.iSerialNumber = -1;
                sReason = "Není zadáno sériové èíslo mìøidla";
                return -1;
            }

            // rozpársuje klienta na výrobce a sn, pokud není výrobce doplní LGZ aby vznikl název souboru ve tvaru LGZ1234.XML
            // Parses the client into manufacturer and SN; if the manufacturer is missing, adds "LGZ" to form a filename in the format LGZ1234.XML.
            ParseString(sSerialNumber, out string letterPart, out int numberPart);

            stBridgeSetting.iSerialNumber = numberPart;

            // 1. Získání absolutní cesty k exe souboru
            // 1. Get the absolute path to the exe file.
            string exePath = AppDomain.CurrentDomain.BaseDirectory;

            // získání jména souboru ze sériového èísla èísla mìøidla
            // Get the file name from the meter's serial number.
            string sFileName = letterPart + numberPart.ToString();

            // 2. Cesta k souborùm txt a xml v podadresáøi "Data"
            // 2. Path to TXT and XML files in the "Data" subdirectory.
            string filePathTXT = Path.Combine(exePath, "Data", sFileName + ".ini");
            string filePathXML = Path.Combine(exePath, "Data", sFileName + ".XML");

            //vrací tuto cestu k xml souboru
            // Returns this path to the XML file.
            string sOutfile = filePathXML;

            Dictionary<string, object> config = new Dictionary<string, object>();

            // 3. Kontrola, zda soubor ini existuje
            // 3. Check if the INI file exists.
            if (File.Exists(filePathTXT))
            {
                stBridgeSetting.sOutfile = filePathXML;
                
                if (MapSecurityLevel(iSecurityLevel, out string AuthName, out string AuthLevel, out sReason) == -1)
                {
                    sReason = "Neexistuje sekce v mapovacím souboru " + sReason + iSecurityLevel;
                    return -1;
                }

                config = LoadConfigForSection(filePathTXT, AuthName);
                config.Add("Authentication", AuthLevel);

            }
            else
            {
                // some default value
                config = new Dictionary<string, object>();
                config.Add("Authentication", "None");
                config.Add("Password", "00000000");
                config.Add("ClientAddress", 32);

                sReason = "Neexistuje ini soubor " + filePathTXT;
                return -1;
            }

            stBridgeSetting.config = config;

            return 1;
        }

        // Finds the corresponding block in Constants.AuthenticationTypes based on the security level, e.g., {"20", "Low", "Low"}.
        // podle security level najde odpovídající blok v Constants.AuthenticationTypes napø. {"20","Low", "Low"}
        private static int  MapSecurityLevel(int iSecurityLevel, out string AuthName, out string AuthLevel, out string  sReason)
        {
            sReason = string.Empty;
            AuthName = string.Empty;
            AuthLevel = string.Empty;

            int rows = Constants.AuthenticationTypes.GetLength(0);

            for (int i = 0; i < rows; i++)
            { 
                if (Constants.AuthenticationTypes[i, 0] == iSecurityLevel.ToString())
                {
                    AuthName = Constants.AuthenticationTypes[i, 1];
                    AuthLevel =  Constants.AuthenticationTypes[i, 2];
                    return 1;
                }
            }

            // Pokud nenajdeme shodu, vrátíme -1
            // If no match is found, return -1.
            sReason = "Neplatná položka security level";
            return -1;
        }

        // Extracts the meter number from LGZ72980038; if it contains only a number, assigns the default value from Constants to letterPart.
        //z LGZ72980038 vyseparuje èíslo mìøidla, pokud má jen èíslo, tak do letterPart dá default z Constatnts
        private static void ParseString(string input, out string letterPart, out int numberPart)
        {
            // String modification
            string inputSN = input.ToString().Trim().Normalize();
            inputSN = inputSN.Replace("\0", ""); // Odstranìní nulových bajtù

            // Default settings
            letterPart = Constants.PRODUCER;
            numberPart = 0;

            if (string.IsNullOrEmpty(input))
            {
                FTisk(1, "Špatný formát seriového èísla mìøidla {0}", input);
                return;
            }
            
            try
            {
                Match match = Regex.Match(inputSN, @"^([A-Za-z]*)(\d+)$");

                if (match.Success)
                {
                    // Letter part
                    string letters = match.Groups[1].Value;

                    if (!string.IsNullOrEmpty(letters))
                    {
                        letterPart = letters;
                    }

                    // Numeric part
                    string numbers = match.Groups[2].Value;
                    Console.WriteLine("Number: " + numbers);

                    if (int.TryParse(numbers, out int result))
                    {
                        numberPart = result;
                    }
                }

            }
            catch(Exception ex)
            {
                FTisk(1, "Špatný formát seriového èísla mìøidla {0}", ex.Message);
            }

        }

        // Returns a dictionary with populated data from the INI file based on the required security level.
        // vrátí slovník s vyplnìnými údaji z ini podle požadované security 
        public static Dictionary<string, object> LoadConfigForSection(string filePath, string sectionName)
        {
           
            // Výsledná struktura pro požadovanou sekci
            var sectionData = new Dictionary<string, object>();
            bool isInDesiredSection = false;

            Console.WriteLine($"sectionName '{sectionName}' ");

            try
            {
                foreach (var line in File.ReadLines(filePath))
                {
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // Remove whitespace characters
                    var trimmedLine = line.Trim();

                    // If the line is empty, skip it
                    if (string.IsNullOrEmpty(trimmedLine))
                        continue;

                    // If the line starts with [, it is a section
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        isInDesiredSection = false;

                        // Check if we are in the desired section
                        var currentSection = trimmedLine.Trim('[', ']');
                        isInDesiredSection = currentSection.Equals(sectionName, StringComparison.OrdinalIgnoreCase);

                        // jestli to nebyla požadovaná sekce tak se zkontroluje spoleèná [General] sekce a ta se pøidá také
                        // If it was not the desired section, check the common [General] section and add it as well
                        if (!isInDesiredSection)
                            isInDesiredSection = currentSection.Equals("General", StringComparison.OrdinalIgnoreCase);
                    }
                    // Pokud jsme v požadované sekci a øádek obsahuje klíè-hodnotu
                    // If we are in the desired section and the line contains a key-value pair
                    else if (isInDesiredSection && trimmedLine.Contains("="))
                    {
                        // Rozdìlení na klíè a hodnotu
                        // Split into key and value
                        var parts = trimmedLine.Split(new[] { '=' }, 2);

                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();

                            // Rozpoznání typu hodnoty
                            // Identify the value type
                            if (value.StartsWith("\"") && value.EndsWith("\""))
                            {
                                // Hodnota je obklopena uvozovkami, odstranit je a uložit jako string
                                // If the value is enclosed in quotes, remove them and store it as a string
                                sectionData[key] = value.Trim('"');
                            }
                            else if (int.TryParse(value, out int intValue))
                            {
                                // Hodnota je validní int
                                // If the value is a valid integer, store it as an int
                                sectionData[key] = intValue;
                            }
                            else
                            {
                                // Ve všech ostatních pøípadech uložit jako string
                                // In all other cases, store it as a string
                                sectionData[key] = value;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Sekce '{sectionName}' nemá správný formát.");
                            return null;
                        }
                    }
                }

                // Kontrola, zda byla nalezena požadovaná sekce
                // Check if the desired section was found.
                if (!isInDesiredSection && sectionData.Count == 0)
                {
                    Console.WriteLine($"Sekce '{sectionName}' nebyla nalezena v souboru.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba pøi ètení souboru: {ex.Message}");
            }

            return sectionData;
        }

        // výpis ve zvoleném vláknu
        // Output in the selected thread.
        private static void FTisk(ushort ThreadNo, string format, params object[] args)
        {
            // !!!!! MARPI doèasnì odstaveno, aby bylo vidìt výpisy i z jiného threadu
            // Temporarily disabled to allow logs from other threads to be visible.
            // if ((SledovanaKomunikace & 0x7FFF) == ThreadNo || ThreadNo == 0)    //posledni bit je priznak pro vypisy DLMS knihoven
            {
                string sub;
                if (format[0] == '*')
                {
                    Console.Write("({0})", ThreadNo);  // Formatting that starts with * outputs (1) communication ID.
                    sub = format.Substring(1);
                }
                else
                {
                    sub = format;
                }
                Console.Write(sub, args);
            }
        }

        // odchytávání událostí klávesnice
        // Capturing keyboard events.
        private static ushort PrikazyKlavesnice(ushort SledovanaKomunikace)
        {
            if (Console.KeyAvailable)
            {
                ushort MemFocused = SledovanaKomunikace;
                char KeyChar = Console.ReadKey().KeyChar;
                switch (KeyChar)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        SledovanaKomunikace = (ushort)(KeyChar - (char)'0');
                        break;
                    case ' ':   // Space moves to the next communication.
                        SledovanaKomunikace++;
                        break;
                    case 'd':   //detailed
                    case 'D':
                        SledovanaKomunikace ^= 0x8000;
                        FTisk(0, "Zmena detailnich info od DLMS komunikace\n");
                        break;
                }
                if ((SledovanaKomunikace & 0x7FFF) > connCounter) SledovanaKomunikace = 0;   // Turn off.
                if (SledovanaKomunikace != MemFocused)
                    FTisk(0, "Sledovana komunikace prepnuta na No:{0}\n", SledovanaKomunikace & 0x7FFF);
            }

            return SledovanaKomunikace;
        }

    }
}
