/*
    Copyright (C) 2015 MadeInTheUSB LLC

    Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
    associated documentation files (the "Software"), to deal in the Software without restriction, 
    including without limitation the rights to use, copy, modify, merge, publish, distribute, 
    sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is 
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all copies or substantial 
    portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
    LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
    IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
    WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE 
    OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MadeInTheUSB;
using MadeInTheUSB.Components;
using MadeInTheUSB.i2c;
using MadeInTheUSB.GPIO;
using MadeInTheUSB.EEPROM;

namespace MadeInTheUSB
{
    /// <summary>
    /// This demo write and read the SPI EEPROM 25AA256 (32k byte of data).
    /// The bytes can be accessed one at the time or per page of 64 bytes.
    /// To test the code we write the 32k byte of data one page at the time.
    /// To test the code we read the 32k byte of data one page at the time or in batch of 8 to 16 pages.
    /// All 64 bytes page have values : 0..64
    /// Except page 2 which all value are 129
    /// Except page 3 which all value are 170
    /// </summary>
    class Demo
    {
        private static FLASH_S25FL1xxK _flash;
        private const byte NEW_WRITTEN_VALUE_1 = 128+1;
        private const byte NEW_WRITTEN_VALUE_2 = 170;

        static string GetAssemblyProduct()
        {
            Assembly currentAssem = typeof(Program).Assembly;
            object[] attribs = currentAssem.GetCustomAttributes(typeof(AssemblyProductAttribute), true);
            if(attribs.Length > 0)
                return  ((AssemblyProductAttribute) attribs[0]).Product;
            return null;
        }

        static void WriteFlashNORMemory(int numberOfPageToRead, int valueToWrite = -1)
        {
            Console.Clear();
            var totalErrorCount = 0;
            var t = Stopwatch.StartNew();

            for (var p = 0; p < _flash.MaxPage; p++)
            {
                var refBuffer = new List<Byte>();
                for (var x = 0; x < _flash.PAGE_SIZE; x++)
                {
                    if (valueToWrite != -1)
                    {
                        refBuffer.Add((byte)valueToWrite);
                    }
                    else
                    {
                        if(p == 2)
                            refBuffer.Add((byte)NEW_WRITTEN_VALUE_1);
                        else if(p == 3)
                            refBuffer.Add((byte)NEW_WRITTEN_VALUE_2);
                        else 
                            refBuffer.Add((byte)x);
                    }
                }

                if(p % 10 == 0)
                    Console.WriteLine("Writing page {0}", p);

                var r = _flash.WritePage(p*_flash.PAGE_SIZE, refBuffer.ToArray());
                if (!r)
                {
                    Console.WriteLine("WriteBuffer failure");
                }
            }
            t.Stop();
            Console.WriteLine("{0} error(s), Time:{1}", totalErrorCount, t.ElapsedMilliseconds);
            Console.WriteLine("Hit enter key");
            Console.ReadLine();
        }

        static void WriteEEPROMByte(int maxPage)
        {
            Console.Clear();
            var totalErrorCount = 0;
            Console.WriteLine("Write first 64 byte one by one");
            var t = Stopwatch.StartNew();
            var p = 2;
            Console.WriteLine("Writing page [{0}]", p);

            for (var i = 0; i < _flash.PAGE_SIZE; i++)
            {
                //Console.WriteLine("Reading byte [{0}]", i);
                var addr     = (p * _flash.PAGE_SIZE ) + i;
                var b = _flash.WriteByte(addr, (byte)(NEW_WRITTEN_VALUE_1+i));
                //Console.WriteLine(String.Format("[{0}] = {1}", i, b));
            }
            Console.WriteLine("{0} error(s), Time:{1}", totalErrorCount, t.ElapsedMilliseconds);
            Console.WriteLine("Hit enter key");
            Console.ReadLine();
        }

        static void ReadEEPROMByte(int maxPage)
        {
            Console.Clear();
            var totalErrorCount = 0;
            Console.WriteLine("Reading first 64 byte one by one");
            var t = Stopwatch.StartNew();

            for (var p = 0; p < maxPage; p++) { 

                Console.WriteLine("Reading page [{0}]", p);

                for (var i = 0; i < _flash.PAGE_SIZE; i++)
                {
                    //Console.WriteLine("Reading byte [{0}]", i);
                    var addr     = (p * _flash.PAGE_SIZE ) + i;
                    var b        = _flash.ReadByte(addr);
                    var expected = i;
                    if (p == 2)
                        expected = NEW_WRITTEN_VALUE_1;
                    if (p == 3)
                        expected = NEW_WRITTEN_VALUE_2;


                    if (b != expected)
                    {
                        Console.WriteLine("Failed  [{0}] = {1}, expected {2}", addr, b, i);
                        totalErrorCount++;
                    }
                    //Console.WriteLine(String.Format("[{0}] = {1}", i, b));
                }
            }
            Console.WriteLine("{0} error(s), Time:{1}", totalErrorCount, t.ElapsedMilliseconds);
            Console.WriteLine("Hit enter key");
            Console.ReadLine();
        }


        static void ReadAndVerifyFlashNORMemory(int numberOfPageToRead, int expectedSingleValue = -1)
        {
            Console.Clear();
            ConsoleEx.TitleBar(0, string.Format("Performance test reading all {0}k no batch standard SPI code", _flash.MaxKByte));
            ConsoleEx.Gotoxy(0, 2);

            var totalErrorCount = 0;
            var t = Stopwatch.StartNew();
            byte [] buf;

            for (var p = 0; p < numberOfPageToRead; p++)
            {
                if(p % 50 == 0 || p < 5)
                    Console.WriteLine("Reading page {0}", p);

                var r = _flash.ReadPage(p * _flash.PAGE_SIZE, _flash.PAGE_SIZE);
                if (r.Succeeded)
                {
                    buf = r.Buffer;
                    for (var i = 0; i < _flash.PAGE_SIZE; i++)
                    {
                        var expected = i;
                        if (p == 2)
                            expected = NEW_WRITTEN_VALUE_1;
                        if (p == 3)
                            expected = NEW_WRITTEN_VALUE_2;

                        if (expectedSingleValue != -1)
                            expected = expectedSingleValue;

                        if (buf[i] != expected)
                        {
                            Console.WriteLine("Failed Page:{0} [{1}] = {2}, expected {3}", p, i, buf[i], expected);
                            totalErrorCount++;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("ReadBuffer failure");
                }
            }
            t.Stop();
            Console.WriteLine("{0} error(s), Data:{1}kb, Time:{2}, {3:0.00} kb/s", totalErrorCount, _flash.MaxKByte, t.ElapsedMilliseconds, _flash.MaxByte*1.0/t.ElapsedMilliseconds);
            Console.WriteLine("Hit enter key");
            Console.ReadLine();
        }
        

        static void ReadAndVerifyEEPROMOnyByteAtTheTime(int numberOfPageToRead)
        {
            Console.Clear();
            var totalErrorCount = 0;
            var t = Stopwatch.StartNew();
            byte [] buf;

            for (var p = 0; p < numberOfPageToRead; p++)
            {
                if(p % 50 == 0 || p < 5)
                    Console.WriteLine("Reading page {0}", p);

                for (var bx = 0; bx < _flash.PAGE_SIZE; bx++)
                {
                    var b = _flash.ReadByte((p*_flash.PAGE_SIZE) + bx);
                    if (b != -1)
                    {
                        var expected = bx;
                        if (p == 2)
                            expected = NEW_WRITTEN_VALUE_1;
                        if (p == 3)
                            expected = NEW_WRITTEN_VALUE_2;

                        if (b != expected)
                        {
                            Console.WriteLine("Failed Page:{0} [{1}] = {2}, expected {3}", p, bx, b, expected);
                            totalErrorCount++;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Read Byte failure");
                    }
                }
            }
            t.Stop();
            
            Console.WriteLine("{0} error(s), Time:{1}, {2:0.00} kb/s", 
                totalErrorCount, 
                t.ElapsedMilliseconds,
                _flash.MaxByte*1.0/t.ElapsedMilliseconds
                );
            Console.WriteLine("Hit enter key");
            Console.ReadLine();
        }

        static void Cls(Nusbio nusbio)
        {
            Console.Clear();

            ConsoleEx.TitleBar(0, GetAssemblyProduct(), ConsoleColor.Yellow, ConsoleColor.DarkBlue);

            ConsoleEx.WriteMenu(-1, 4, "R)ead all   W)rite all");
            ConsoleEx.WriteMenu(-1, 6, "Q)uit");
           
            ConsoleEx.TitleBar(ConsoleEx.WindowHeight-2, Nusbio.GetAssemblyCopyright(), ConsoleColor.White, ConsoleColor.DarkBlue);
            ConsoleEx.Bar(0, ConsoleEx.WindowHeight-3, string.Format("Nusbio SerialNumber:{0}, Description:{1}", nusbio.SerialNumber, nusbio.Description), ConsoleColor.Black, ConsoleColor.DarkCyan);
        }

        public static void Run(string[] args)
        {
            Console.WriteLine("Nusbio initialization");
            var serialNumber = Nusbio.Detect();
            
            if (serialNumber == null) // Detect the first Nusbio available
            {
                Console.WriteLine("nusbio not detected");
                return;
            }

            using (var nusbio = new Nusbio(serialNumber))
            {
                Cls(nusbio);

                _flash = new FLASH_S25FL1xxK(
                    nusbio   : nusbio,
                    clockPin : NusbioGpio.Gpio0,
                    mosiPin  : NusbioGpio.Gpio1,
                    misoPin  : NusbioGpio.Gpio2,
                    selectPin: NusbioGpio.Gpio3
                    );

                _flash.Begin();
                

                var r1 = _flash.GetRegister1();

                while (nusbio.Loop())
                {
                    if (Console.KeyAvailable)
                    {
                        var k = Console.ReadKey(true).Key;
                            
                        if (k == ConsoleKey.W)
                        {
                            var a = ConsoleEx.Question(23, "Execute Write/Read/Write Test 2k now Y)es, N)o", new List<char>()  { 'Y', 'N' });
                            if (a == 'Y') {
                                var testValue = 1+4+16+64;
                                WriteFlashNORMemory(_flash.MaxPage, testValue);
                                ReadAndVerifyFlashNORMemory(_flash.MaxPage, testValue);
                                WriteFlashNORMemory(_flash.MaxPage);
                                ReadAndVerifyFlashNORMemory(_flash.MaxPage);
                            }
                        }

                        if (k == ConsoleKey.R)
                        {
                            ReadAndVerifyFlashNORMemory(_flash.MaxPage);
                        }

                        if (k == ConsoleKey.Q) break;

                        Cls(nusbio);
                    }
                }
            }
            Console.Clear();
        }
    }
}
