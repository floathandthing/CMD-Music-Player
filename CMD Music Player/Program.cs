﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CMD_Music_Player
{
    static class Program
    {
        public static WMPLib.WindowsMediaPlayer player = new WMPLib.WindowsMediaPlayer(); //the heart of this program
        
        static bool bypassfilecheck = false;
        public static void error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.White;
        }
        static bool IsMediaSelected()
        {
            bool answer;
            try
            {
                string x = player.currentMedia.durationString;
                answer = true;
            }
            catch { answer = false; }
            return answer;
        }
        static string CreateBarFromMediaInfo()
        {
            //very simple; just returns a progress bar in a certain layout with the media information.
            //this function only exists to prevent having to enter this very long command to get a standardised progress bar.
            return ProgressBar.GenerateBar(player.controls.currentPosition,
                                           player.currentMedia.duration,
                                           barwidth: Console.WindowWidth - 2,
                                           barsuffix: "] " + player.controls.currentPositionString + "/" + player.currentMedia.durationString);
        }

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;

            //scan list of folders
            Console.WriteLine("Initialising music index...");
            List<string> registeredfolders = new List<string> { }; //empty list to hold list of folders containing music
            foreach (string path in
                Properties.Settings.Default.RegisteredFolders.Split(
                    new char[] { '^', '^', '^' }, //folder entries are separated by three carats.
                    StringSplitOptions.RemoveEmptyEntries
                )
            ) registeredfolders.Add(path); //reconstruct list of folder paths from stored string
            //check for music in the application folder if enabled.
            if (Properties.Settings.Default.RegisterAppDirectory) registeredfolders.Add(AppDomain.CurrentDomain.BaseDirectory);

            //Perform a recursive search on each folder for music files
            Console.WriteLine("Scanning registered folders...");
            List<string> discoveredfiles = new List<string> { };
            foreach (string folder in registeredfolders)
            {
                foreach (string file in DataSearch.ScanForMedia(folder))
                {
                    discoveredfiles.Add(file);
                }
            }
            Console.WriteLine(discoveredfiles.Count + " files found.");

            for (; ; )
            {
                Console.Write(">");
                string[] command = CommandLineToArgs(Console.ReadLine());
                if (command.Length == 0) continue;
                string commandupper = command[0].ToUpper();

                if (commandupper == "HELP")
                {
                    foreach (string line in new string[] { "List of commands:" ,
                                             "\tplay" ,
                                             "\tstop" ,
                                             "\tpause" ,
                                             "" ,
                                             "\tpos" ,
                                             "\tgoto" ,
                                             "",
                                            "\tls" ,
                                            "\texit"})
                    { Console.WriteLine(line); }
                }
                else if (commandupper == "LS")
                {
                    foreach (string file in discoveredfiles)
                        Console.WriteLine("\t" + Path.GetFileName(file));
                }
                else if (commandupper == "PLAY")
                {
                    if (command.Length == 1) { player.controls.play(); }
                    else
                    {
                        string path = command[1];
                        //remove quotation marks, as they interfere with the media player.
                        path = path.Replace(Convert.ToChar("\""), Convert.ToChar(" "));
                        if (File.Exists(path) || bypassfilecheck) //check if either the file exists, or if said check is disabled.
                        {
                            Console.WriteLine("Playing " + path + "...");
                            try
                            {
                                player.URL = path;
                                player.controls.play();
                            }
                            catch (Exception err) { error("Error playing file: " + err.Message); }
                        }
                        else { error("Error playing file: File doesn't exist."); }
                    }
                }
                else if (commandupper == "STOP") { player.controls.stop(); }
                else if (commandupper == "PAUSE") { player.controls.pause(); }
                else if (commandupper == "POS")
                {
                    if (IsMediaSelected())
                    {
                        for (int i = 0; i < player.currentMedia.attributeCount; i++)
                        {
                            var name = player.currentMedia.getAttributeName(i);
                            var value = player.currentMedia.getItemInfo(name);
                            if (value.Length == 0) continue;
                            Console.WriteLine(name + ": " + value);
                        }

                        Console.WriteLine(CreateBarFromMediaInfo());
                    }
                    else { error("No media is loaded."); }

                }
                else if (commandupper == "GOTO")
                {
                    string syntax = "Syntax: [hh:][mm:]ss";
                    if (!IsMediaSelected())
                    {
                        error("No media is selected!");
                        continue;
                    }
                    if (command.Length < 2)
                    {
                        error(syntax);
                        continue;
                    }

                    string[] strtimeargs = command[1].Split(Char.Parse(":"));
                    if (strtimeargs.Length < 1 || strtimeargs.Length > 3) error(syntax);
                    strtimeargs.Reverse(); //reverse it so it is now in the ss:mm:hh format. This makes it easier to process.
                    List<double> timeargs = new List<double>();

                    try
                    {
                        foreach (string item in strtimeargs)
                        {
                            if (item == "") { error(syntax); continue; }
                            timeargs.Add(Convert.ToDouble(item)); //double is bulletproof since it is a 64 bit number, so it will overflow after 5.38 millenia.
                        }
                    }
                    catch (Exception err) { error("Invalid time code: " + err.Message); continue; }
                    //parse into seconds
                    float seconds = 0;
                    int multiplier = 1; //multiply the item by this
                    foreach (int unit in timeargs) {
                        seconds += unit * multiplier;
                        multiplier *= 60; //multiple the multipler by 60 (sequence is 1,60,3600)
                    }
                    //is this longer than the length of the song?
                    if (seconds > player.currentMedia.duration) error("Timecode is below 0 seconds.");
                    //or is it smaller than 0 ? 
                    player.controls.currentPosition = seconds;

                    Console.WriteLine(CreateBarFromMediaInfo());
                }

                else if (commandupper == "TOGGLE_FILECHECK")
                {
                    //just for fun: disables checking of existence of a file. this could allow for lots of possible input, such as http addresses pointing to media
                    bypassfilecheck = !bypassfilecheck;
                    Console.WriteLine(!bypassfilecheck);
                }
                else if (commandupper == "EXIT") { Environment.Exit(0); }
                else { error("Command unknown. Type 'help' for a list of commands."); }
            }
        }


        //https://stackoverflow.com/questions/298830/split-string-containing-command-line-parameters-into-string-in-c-sharp
        [DllImport("shell32.dll", SetLastError = true)]
        static extern IntPtr CommandLineToArgvW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs
        );
        public static string[] CommandLineToArgs(string commandLine)
        {
            var argv = CommandLineToArgvW(commandLine, out int argc);
            if (argv == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception();
            try
            {
                var args = new string[argc];
                for (var i = 0; i < args.Length; i++)
                {
                    var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(p);
                }

                return args;
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
            }
        }
    }

    static class DataSearch
    {
        public static List<string> ScanForMedia(string searchfolder, bool recursive = true, bool verbose = false)
        {
            List<string> found_files = new List<string> { }; //list to store discovered files.

            //check if we have permission to access the folder
            try { Directory.GetFiles(searchfolder); }
            catch { return new List<string> { }; }

            //add all the files in the folder that are music files.
            foreach (string filepath in Directory.GetFiles(searchfolder))
                {
                    if (IsSupportedFiletype(filepath))
                    {
                        if (verbose) Console.WriteLine("Discovered " + filepath);
                        found_files.Add(filepath);
                    }
                }

            //recursively check all subdirectories if flag was set
            if (recursive)
            {
                foreach (string folderpath in Directory.GetDirectories(searchfolder))
                {
                    if (verbose) Console.WriteLine("Traversing " + folderpath);
                    List<string> sub_files = ScanForMedia(folderpath);
                    foreach (string file in sub_files)
                    {
                        if (IsSupportedFiletype(file)) found_files.Add(file);
                        
                    }
                }
            }

            //return the list of discovered files
            if (verbose) Console.WriteLine(found_files.Count + " files found.");
            return found_files;
        }
        static bool IsSupportedFiletype(string filename)
        {
            string[] supportedformats = new string[] { ".3g2", ".3gp", ".3gp2", ".3gpp", ".aac", ".adt", ".adts", ".aif", ".aifc", ".aiff", ".asf", ".asx", ".au", ".avi", ".cda", ".dvr-ms", ".flac", ".ivf", ".m1v", ".m2ts", ".m3u", ".m4a", ".m4v", ".mid", ".midi", ".mov", ".mp2", ".mp3", ".mp4", ".mp4v", ".mpa", ".mpe", ".mpeg", ".mpg", ".rmi", ".snd", ".wav", ".wax", ".wm", ".wma", ".wmd", ".wms", ".wmv", ".wmx", ".wmz", ".wpl", ".wvx" };
            foreach (string type in supportedformats)
            {
                if (filename.EndsWith(type)) return true;
            }
            return false;
        }
    }
    static class ProgressBar
    {
        static decimal Map(this decimal value, decimal fromSource, decimal toSource, decimal fromTarget, decimal toTarget)
        {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }

        //this was converted by hand from Python to C#.
        public static string GenerateBar(double value, double maxvalue,
            string barprefix = "[", string filledchar = "=", string pointerchar = ">",
            string emptychar = " ", string barsuffix = "]", int barwidth = 50)
        {
            //sanity checks to make sure nothing is going to break anything
            if (value > maxvalue)
            {
                Program.error("Current value is bigger than max value!");
                return "";
            }
            if (value < 0)
            {
                Program.error("Current value is smaller than 0!");
                return "";
            }
            if (maxvalue == 0)
            {
                Program.error("Error generating bar: max length is 0." +
                    "");
                return "";
            }

            decimal percentage = (decimal)value / (decimal)maxvalue * 100; //calculate the percentage of progress
                                                                           //calculate how much space the non-changing parts of the bar will take up
            int length = (
                barprefix.Length +
                pointerchar.Length +
                barsuffix.Length
            );

            //If that length is smaller than the barwidth, then there will literally be no space left for the bar to move. Oh well.
            //adjust the bar to fit the space it has, using the length we just calculated
            value = Convert.ToInt32(Map((decimal)value, 0, (decimal)maxvalue, 0, barwidth - length));
            maxvalue = barwidth - length;

            //construct the bar as a string
            string bar = barprefix; //start with the bar prefix
            bar += String.Concat(Enumerable.Repeat(filledchar, (int)value)); //the currently filled portion of the bar
            bar += pointerchar;
            bar += String.Concat(Enumerable.Repeat(emptychar, (int)maxvalue - (int)value)); //the empty portion of the bar
            bar += barsuffix + " "; //separator

            return bar; //send the finished thing back

        }
    }
}