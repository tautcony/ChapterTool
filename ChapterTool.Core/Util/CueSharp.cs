/*
Title:    CueSharp
Version:  0.5
Released: March 24, 2007

Author:   Wyatt O'Day
Website:  wyday.com/cuesharp
*/

namespace ChapterTool.Util
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using ChapterTool.Util.Cue.Types;

    /// <summary>
    /// A CueSheet class used to create, open, edit, and save cuesheets.
    /// </summary>
    public class CueSheet
    {
        #region Private Variables

        private string[] _cueLines;

        // strings that don't belong or were mistyped in the global part of the cue
        #endregion Private Variables

        #region Properties

        /// <summary>
        /// Returns/Sets track in this cuefile.
        /// </summary>
        /// <param name="tracknumber">The track in this cuefile.</param>
        /// <returns>Track at the tracknumber.</returns>
        public Track this[int tracknumber]
        {
            get => Tracks[tracknumber];
            set => Tracks[tracknumber] = value;
        }

        /// <summary>
        /// The catalog number must be 13 digits long and is encoded according to UPC/EAN rules.
        /// Example: CATALOG 1234567890123
        /// </summary>
        public string Catalog { get; set; } = string.Empty;

        /// <summary>
        /// This command is used to specify the name of the file that contains the encoded CD-TEXT information for the disc. This command is only used with files that were either created with the graphical CD-TEXT editor or generated automatically by the software when copying a CD-TEXT enhanced disc.
        /// </summary>
        public string CDTextFile { get; set; } = string.Empty;

        /// <summary>
        /// This command is used to put comments in your CUE SHEET file.
        /// </summary>
        public string[] Comments { get; set; } = new string[0];

        /// <summary>
        /// Lines in the cue file that don't belong or have other general syntax errors.
        /// </summary>
        public string[] Garbage { get; private set; } = new string[0];

        /// <summary>
        /// This command is used to specify the name of a perfomer for a CD-TEXT enhanced disc.
        /// </summary>
        public string Performer { get; set; } = string.Empty;

        /// <summary>
        /// This command is used to specify the name of a songwriter for a CD-TEXT enhanced disc.
        /// </summary>
        public string Songwriter { get; set; } = string.Empty;

        /// <summary>
        /// The title of the entire disc as a whole.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The array of tracks on the cuesheet.
        /// </summary>
        public Track[] Tracks { get; set; } = new Track[0];

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Create a cue sheet from scratch.
        /// </summary>
        public CueSheet()
        {
        }

        /// <summary>
        /// Parse a cue sheet string.
        /// </summary>
        /// <param name="cueString">A string containing the cue sheet data.</param>
        /// <param name="lineDelims">Line delimeters; set to "(char[])null" for default delimeters.</param>
        public CueSheet(string cueString, char[] lineDelims = null)
        {
            if (lineDelims == null)
            {
                lineDelims = new[] { '\n' };
            }

            _cueLines = cueString.Split(lineDelims);
            RemoveEmptyLines(ref _cueLines);
            ParseCue(_cueLines);
        }

        /// <summary>
        /// Parses a cue sheet file.
        /// </summary>
        /// <param name="cuefilename">The filename for the cue sheet to open.</param>
        public CueSheet(string cuefilename)
        {
            ReadCueSheet(cuefilename, Encoding.Default);
        }

        /// <summary>
        /// Parses a cue sheet file.
        /// </summary>
        /// <param name="cuefilename">The filename for the cue sheet to open.</param>
        /// <param name="encoding">The encoding used to open the file.</param>
        public CueSheet(string cuefilename, Encoding encoding)
        {
            ReadCueSheet(cuefilename, encoding);
        }

        private void ReadCueSheet(string filename, Encoding encoding)
        {
            // array of delimiters to split the sentence with
            char[] delimiters = { '\n' };

            // read in the full cue file
            TextReader tr = new StreamReader(filename, encoding);

            // read in file
            _cueLines = tr.ReadToEnd().Split(delimiters);

            // close the stream
            tr.Close();

            RemoveEmptyLines(ref _cueLines);

            ParseCue(_cueLines);
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Removes any empty lines, elimating possible trouble.
        /// </summary>
        /// <param name="file"></param>
        private void RemoveEmptyLines(ref string[] file)
        {
            var itemsRemoved = 0;

            for (var i = 0; i < file.Length; i++)
            {
                if (file[i].Trim() != string.Empty)
                {
                    file[i - itemsRemoved] = file[i];
                }
                else if (file[i].Trim() == string.Empty)
                {
                    itemsRemoved++;
                }
            }

            if (itemsRemoved > 0)
            {
                file = (string[])ResizeArray(file, file.Length - itemsRemoved);
            }
        }

        private void ParseCue(string[] file)
        {
            // -1 means still global,
            // all others are track specific
            var trackOn = -1;
            var currentFile = default(AudioFile);

            for (var i = 0; i < file.Length; i++)
            {
                file[i] = file[i].Trim();

                switch (file[i].Substring(0, file[i].IndexOf(' ')).ToUpper())
                {
                    case "CATALOG":
                        ParseString(file[i], trackOn);
                        break;

                    case "CDTEXTFILE":
                        ParseString(file[i], trackOn);
                        break;

                    case "FILE":
                        currentFile = ParseFile(file[i], trackOn);
                        break;

                    case "FLAGS":
                        ParseFlags(file[i], trackOn);
                        break;

                    case "INDEX":
                        ParseIndex(file[i], trackOn);
                        break;

                    case "ISRC":
                        ParseString(file[i], trackOn);
                        break;

                    case "PERFORMER":
                        ParseString(file[i], trackOn);
                        break;

                    case "POSTGAP":
                        ParseIndex(file[i], trackOn);
                        break;

                    case "PREGAP":
                        ParseIndex(file[i], trackOn);
                        break;

                    case "REM":
                        ParseComment(file[i], trackOn);
                        break;

                    case "SONGWRITER":
                        ParseString(file[i], trackOn);
                        break;

                    case "TITLE":
                        ParseString(file[i], trackOn);
                        break;

                    case "TRACK":
                        trackOn++;
                        ParseTrack(file[i], trackOn);

                        // if there's a file
                        if (currentFile.Filename != string.Empty)
                        {
                            Tracks[trackOn].DataFile = currentFile;
                            currentFile = default(AudioFile);
                        }
                        break;

                    default:
                        ParseGarbage(file[i], trackOn);

                        // save discarded junk and place string[] with track it was found in
                        break;
                }
            }
        }

        private void ParseComment(string line, int trackOn)
        {
            // remove "REM" (we know the line has already been .Trim()'ed)
            line = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            if (trackOn == -1)
            {
                if (line.Trim() != string.Empty)
                {
                    Comments = (string[])ResizeArray(Comments, Comments.Length + 1);
                    Comments[Comments.Length - 1] = line;
                }
            }
            else
            {
                Tracks[trackOn].AddComment(line);
            }
        }

        private static AudioFile ParseFile(string line, int trackOn)
        {
            line = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            var fileType = line.Substring(line.LastIndexOf(' '), line.Length - line.LastIndexOf(' ')).Trim();

            line = line.Substring(0, line.LastIndexOf(' ')).Trim();

            // if quotes around it, remove them.
            if (line[0] == '"')
            {
                line = line.Substring(1, line.LastIndexOf('"') - 1);
            }

            return new AudioFile(line, fileType);
        }

        private void ParseFlags(string line, int trackOn)
        {
            if (trackOn != -1)
            {
                line = line.Trim();
                if (line != string.Empty)
                {
                    string temp;
                    try
                    {
                        temp = line.Substring(0, line.IndexOf(' ')).ToUpper();
                    }
                    catch (Exception)
                    {
                        temp = line.ToUpper();
                    }

                    switch (temp)
                    {
                        case "FLAGS":
                            Tracks[trackOn].AddFlag(temp);
                            break;

                        case "DATA":
                            Tracks[trackOn].AddFlag(temp);
                            break;

                        case "DCP":
                            Tracks[trackOn].AddFlag(temp);
                            break;

                        case "4CH":
                            Tracks[trackOn].AddFlag(temp);
                            break;

                        case "PRE":
                            Tracks[trackOn].AddFlag(temp);
                            break;

                        case "SCMS":
                            Tracks[trackOn].AddFlag(temp);
                            break;
                    }

                    // processing for a case when there isn't any more spaces
                    // i.e. avoiding the "index cannot be less than zero" error
                    // when calling line.IndexOf(' ')
                    try
                    {
                        temp = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' '));
                    }
                    catch (Exception)
                    {
                        temp = line.Substring(0, line.Length);
                    }

                    // if the flag hasn't already been processed
                    if (temp.ToUpper().Trim() != line.ToUpper().Trim())
                    {
                        ParseFlags(temp, trackOn);
                    }
                }
            }
        }

        private void ParseGarbage(string line, int trackOn)
        {
            if (trackOn == -1)
            {
                if (line.Trim() != string.Empty)
                {
                    Garbage = (string[])ResizeArray(Garbage, Garbage.Length + 1);
                    Garbage[Garbage.Length - 1] = line;
                }
            }
            else
            {
                Tracks[trackOn].AddGarbage(line);
            }
        }

        private void ParseIndex(string line, int trackOn)
        {
            var number = 0;

            var indexType = line.Substring(0, line.IndexOf(' ')).ToUpper();

            var tempString = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            if (indexType == "INDEX")
            {
                // read the index number
                number = Convert.ToInt32(tempString.Substring(0, tempString.IndexOf(' ')));
                tempString = tempString.Substring(tempString.IndexOf(' '), tempString.Length - tempString.IndexOf(' ')).Trim();
            }

            // extract the minutes, seconds, and frames
            var minutes = Convert.ToInt32(tempString.Substring(0, tempString.IndexOf(':')));
            var seconds = Convert.ToInt32(tempString.Substring(tempString.IndexOf(':') + 1, tempString.LastIndexOf(':') - tempString.IndexOf(':') - 1));
            var frames = Convert.ToInt32(tempString.Substring(tempString.LastIndexOf(':') + 1, tempString.Length - tempString.LastIndexOf(':') - 1));

            if (indexType == "INDEX")
            {
                Tracks[trackOn].AddIndex(number, minutes, seconds, frames);
            }
            else if (indexType == "PREGAP")
            {
                Tracks[trackOn].PreGap = new Index(0, minutes, seconds, frames);
            }
            else if (indexType == "POSTGAP")
            {
                Tracks[trackOn].PostGap = new Index(0, minutes, seconds, frames);
            }
        }

        private void ParseString(string line, int trackOn)
        {
            var category = line.Substring(0, line.IndexOf(' ')).ToUpper();

            line = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            // get rid of the quotes
            if (line[0] == '"')
            {
                line = line.Substring(1, line.LastIndexOf('"') - 1);
            }

            switch (category)
            {
                case "CATALOG":
                    if (trackOn == -1)
                    {
                        Catalog = line;
                    }
                    break;

                case "CDTEXTFILE":
                    if (trackOn == -1)
                    {
                        CDTextFile = line;
                    }
                    break;

                case "ISRC":
                    if (trackOn != -1)
                    {
                        Tracks[trackOn].ISRC = line;
                    }
                    break;

                case "PERFORMER":
                    if (trackOn == -1)
                    {
                        Performer = line;
                    }
                    else
                    {
                        Tracks[trackOn].Performer = line;
                    }
                    break;

                case "SONGWRITER":
                    if (trackOn == -1)
                    {
                        Songwriter = line;
                    }
                    else
                    {
                        Tracks[trackOn].Songwriter = line;
                    }
                    break;

                case "TITLE":
                    if (trackOn == -1)
                    {
                        Title = line;
                    }
                    else
                    {
                        Tracks[trackOn].Title = line;
                    }
                    break;
            }
        }

        /// <summary>
        /// Parses the TRACK command.
        /// </summary>
        /// <param name="line">The line in the cue file that contains the TRACK command.</param>
        /// <param name="trackOn">The track currently processing.</param>
        private void ParseTrack(string line, int trackOn)
        {
            var tempString = line.Substring(line.IndexOf(' '), line.Length - line.IndexOf(' ')).Trim();

            var trackNumber = Convert.ToInt32(tempString.Substring(0, tempString.IndexOf(' ')));

            // find the data type.
            tempString = tempString.Substring(tempString.IndexOf(' '), tempString.Length - tempString.IndexOf(' ')).Trim();

            AddTrack(trackNumber, tempString);
        }

        /// <summary>
        /// Reallocates an array with a new size, and copies the contents
        /// of the old array to the new array.
        /// </summary>
        /// <param name="oldArray">The old array, to be reallocated.</param>
        /// <param name="newSize">The new array size.</param>
        /// <returns>A new array with the same contents.</returns>
        /// <remarks >Useage: int[] a = {1,2,3}; a = (int[])ResizeArray(a,5);</remarks>
        public static Array ResizeArray(Array oldArray, int newSize)
        {
            var oldSize = oldArray.Length;
            var elementType = oldArray.GetType().GetElementType();
            var newArray = Array.CreateInstance(elementType, newSize);
            var preserveLength = Math.Min(oldSize, newSize);
            if (preserveLength > 0)
                Array.Copy(oldArray, newArray, preserveLength);
            return newArray;
        }

        /// <summary>
        /// Add a track to the current cuesheet.
        /// </summary>
        /// <param name="tracknumber">The number of the said track.</param>
        /// <param name="datatype">The datatype of the track.</param>
        private void AddTrack(int tracknumber, string datatype)
        {
            Tracks = (Track[])ResizeArray(Tracks, Tracks.Length + 1);
            Tracks[Tracks.Length - 1] = new Track(tracknumber, datatype);
        }

        /// <summary>
        /// Add a track to the current cuesheet
        /// </summary>
        /// <param name="title">The title of the track.</param>
        /// <param name="performer">The performer of this track.</param>
        public void AddTrack(string title, string performer)
        {
            Tracks = (Track[])ResizeArray(Tracks, Tracks.Length + 1);
            Tracks[Tracks.Length - 1] = new Track(Tracks.Length, string.Empty)
            {
                Performer = performer,
                Title = title,
            };
        }

        public void AddTrack(string title, string performer, string filename, FileType fType)
        {
            Tracks = (Track[])ResizeArray(Tracks, Tracks.Length + 1);
            Tracks[Tracks.Length - 1] = new Track(Tracks.Length, string.Empty)
            {
                Performer = performer,
                Title = title,
                DataFile = new AudioFile(filename, fType),
            };
        }

        /// <summary>
        /// Add a track to the current cuesheet
        /// </summary>
        /// <param name="title">The title of the track.</param>
        /// <param name="performer">The performer of this track.</param>
        /// <param name="datatype">The datatype for the track (typically DataType.Audio)</param>
        public void AddTrack(string title, string performer, DataType datatype)
        {
            Tracks = (Track[])ResizeArray(Tracks, Tracks.Length + 1);
            Tracks[Tracks.Length - 1] = new Track(Tracks.Length, datatype)
            {
                Performer = performer,
                Title = title,
            };
        }

        /// <summary>
        /// Add a track to the current cuesheet
        /// </summary>
        /// <param name="track">Track object to add to the cuesheet.</param>
        public void AddTrack(Track track)
        {
            Tracks = (Track[])ResizeArray(Tracks, Tracks.Length + 1);
            Tracks[Tracks.Length - 1] = track;
        }

        /// <summary>
        /// Remove a track from the cuesheet.
        /// </summary>
        /// <param name="trackIndex">The index of the track you wish to remove.</param>
        public void RemoveTrack(int trackIndex)
        {
            for (var i = trackIndex; i < Tracks.Length - 1; i++)
            {
                Tracks[i] = Tracks[i + 1];
            }
            Tracks = (Track[])ResizeArray(Tracks, Tracks.Length - 1);
        }

        /// <summary>
        /// Add index information to an existing track.
        /// </summary>
        /// <param name="trackIndex">The array index number of track to be modified</param>
        /// <param name="indexNum">The index number of the new index</param>
        /// <param name="minutes">The minute value of the new index</param>
        /// <param name="seconds">The seconds value of the new index</param>
        /// <param name="frames">The frames value of the new index</param>
        public void AddIndex(int trackIndex, int indexNum, int minutes, int seconds, int frames)
        {
            Tracks[trackIndex].AddIndex(indexNum, minutes, seconds, frames);
        }

        /// <summary>
        /// Remove an index from a track.
        /// </summary>
        /// <param name="trackIndex">The array-index of the track.</param>
        /// <param name="indexIndex">The index of the Index you wish to remove.</param>
        public void RemoveIndex(int trackIndex, int indexIndex)
        {
            // Note it is the index of the Index you want to delete,
            // which may or may not correspond to the number of the index.
            Tracks[trackIndex].RemoveIndex(indexIndex);
        }

        /// <summary>
        /// Save the cue sheet file to specified location.
        /// </summary>
        /// <param name="filename">Filename of destination cue sheet file.</param>
        public void SaveCue(string filename)
        {
            SaveCue(filename, Encoding.Default);
        }

        /// <summary>
        /// Save the cue sheet file to specified location.
        /// </summary>
        /// <param name="filename">Filename of destination cue sheet file.</param>
        /// <param name="encoding">The encoding used to save the file.</param>
        public void SaveCue(string filename, Encoding encoding)
        {
            TextWriter tw = new StreamWriter(filename, false, encoding);

            tw.WriteLine(ToString());

            // close the writer stream
            tw.Close();
        }

        /// <summary>
        /// Method to output the cuesheet into a single formatted string.
        /// </summary>
        /// <returns>The entire cuesheet formatted to specification.</returns>
        public override string ToString()
        {
            var output = new StringBuilder();

            foreach (var comment in Comments)
            {
                output.Append("REM " + comment + Environment.NewLine);
            }

            if (Catalog.Trim() != string.Empty)
            {
                output.Append("CATALOG " + Catalog + Environment.NewLine);
            }

            if (Performer.Trim() != string.Empty)
            {
                output.Append("PERFORMER \"" + Performer + "\"" + Environment.NewLine);
            }

            if (Songwriter.Trim() != string.Empty)
            {
                output.Append("SONGWRITER \"" + Songwriter + "\"" + Environment.NewLine);
            }

            if (Title.Trim() != string.Empty)
            {
                output.Append("TITLE \"" + Title + "\"" + Environment.NewLine);
            }

            if (CDTextFile.Trim() != string.Empty)
            {
                output.Append("CDTEXTFILE \"" + CDTextFile.Trim() + "\"" + Environment.NewLine);
            }

            for (var i = 0; i < Tracks.Length; i++)
            {
                output.Append(Tracks[i].ToString());

                if (i != Tracks.Length - 1)
                {
                    // add line break for each track except last
                    output.Append(Environment.NewLine);
                }
            }

            return output.ToString();
        }

        #endregion Methods

        // TODO: Fix calculation bugs; currently generates erroneous IDs.
        #region CalculateDiscIDs

        // For complete CDDB/freedb discID calculation, see:
        // http://www.freedb.org/modules.php?name=Sections&sop=viewarticle&artid=6
        public string CalculateCDDBdiscID()
        {
            var n = 0;

            /* For backward compatibility this algorithm must not change */

            var i = 0;

            while (i < Tracks.Length)
            {
                n = n + cddb_sum((LastTrackIndex(Tracks[i]).Minutes * 60) + LastTrackIndex(Tracks[i]).Seconds);
                i++;
            }

            Console.WriteLine(n.ToString());

            var t = ((LastTrackIndex(Tracks[Tracks.Length - 1]).Minutes * 60) + LastTrackIndex(Tracks[Tracks.Length - 1]).Seconds) -
                    ((LastTrackIndex(Tracks[0]).Minutes * 60) + LastTrackIndex(Tracks[0]).Seconds);

            ulong lDiscId = ((((uint)n % 0xff) << 24) | ((uint)t << 8) | (uint)Tracks.Length);
            return $"{lDiscId:x8}";
        }

        private static Cue.Types.Index LastTrackIndex(Track track)
        {
            return track.Indices[track.Indices.Length - 1];
        }

        private static int cddb_sum(int n)
        {
            /* For backward compatibility this algorithm must not change */

            var ret = 0;

            while (n > 0)
            {
                ret = ret + (n % 10);
                n = n / 10;
            }

            return ret;
        }

        #endregion CalculateDiscIDs

        public ChapterInfo ToChapterInfo()
        {
            var info = new ChapterInfo
            {
                Title = Title,
                SourceType = "CUE",
                Tag = this,
                TagType = typeof(CueSheet),
            };
            foreach (var track in Tracks)
            {
                string name = $"{track.Title} [{track.Performer}]";
                var time = track.Index01;
                info.Chapters.Add(new Chapter(name, time, track.TrackNumber));
            }
            info.Duration = info.Chapters.Last().Time;
            return info;
        }
    }

    namespace Cue.Types
    {
        /// <summary>
        /// DCP - Digital copy permitted
        /// 4CH - Four channel audio
        /// PRE - Pre-emphasis enabled (audio tracks only)
        /// SCMS - Serial copy management system (not supported by all recorders)
        /// There is a fourth subcode flag called "DATA" which is set for all non-audio tracks. This flag is set automatically based on the datatype of the track.
        /// </summary>
        public enum Flags
        {
            DCP,
            CH4,
            PRE,
            SCMS,
            DATA,
            NONE,
        }

        /// <summary>
        /// BINARY - Intel binary file (least significant byte first)
        /// MOTOROLA - Motorola binary file (most significant byte first)
        /// AIFF - Audio AIFF file
        /// WAVE - Audio WAVE file
        /// MP3 - Audio MP3 file
        /// </summary>
        public enum FileType
        {
            BINARY,
            MOTOROLA,
            AIFF,
            WAVE,
            MP3,
        }

        /// <summary>
        /// <list>
        /// <item>AUDIO - Audio/Music (2352)</item>
        /// <item>CDG - Karaoke CD+G (2448)</item>
        /// <item>MODE1/2048 - CDROM Mode1 Data (cooked)</item>
        /// <item>MODE1/2352 - CDROM Mode1 Data (raw)</item>
        /// <item>MODE2/2336 - CDROM-XA Mode2 Data</item>
        /// <item>MODE2/2352 - CDROM-XA Mode2 Data</item>
        /// <item>CDI/2336 - CDI Mode2 Data</item>
        /// <item>CDI/2352 - CDI Mode2 Data</item>
        /// </list>
        /// </summary>
        public enum DataType
        {
            AUDIO,
            CDG,
            MODE1_2048,
            MODE1_2352,
            MODE2_2336,
            MODE2_2352,
            CDI_2336,
            CDI_2352,
        }

        /// <summary>
        /// This command is used to specify indexes (or subindexes) within a track.
        /// Syntax:
        ///  INDEX [number] [mm:ss:ff]
        /// </summary>
        public struct Index
        {
            // 0-99
            private int _number;

            private int _minutes;
            private int _seconds;
            private int _frames;

            /// <summary>
            /// Index number (0-99)
            /// </summary>
            public int Number
            {
                get => _number;
                set
                {
                    if (value > 99)
                    {
                        _number = 99;
                    }
                    else if (value < 0)
                    {
                        _number = 0;
                    }
                    else
                    {
                        _number = value;
                    }
                }
            }

            /// <summary>
            /// Possible values: 0-99
            /// </summary>
            public int Minutes
            {
                get => _minutes;
                set
                {
                    if (value > 99)
                    {
                        _minutes = 99;
                    }
                    else if (value < 0)
                    {
                        _minutes = 0;
                    }
                    else
                    {
                        _minutes = value;
                    }
                }
            }

            /// <summary>
            /// Possible values: 0-59
            /// There are 60 seconds/minute
            /// </summary>
            public int Seconds
            {
                get => _seconds;
                set
                {
                    if (value >= 60)
                    {
                        _seconds = 59;
                    }
                    else if (value < 0)
                    {
                        _seconds = 0;
                    }
                    else
                    {
                        _seconds = value;
                    }
                }
            }

            /// <summary>
            /// Possible values: 0-74
            /// There are 75 frames/second
            /// </summary>
            public int Frames
            {
                get => _frames;
                set
                {
                    if (value >= 75)
                    {
                        _frames = 74;
                    }
                    else if (value < 0)
                    {
                        _frames = 0;
                    }
                    else
                    {
                        _frames = value;
                    }
                }
            }

            /// <summary>
            /// The Index of a track.
            /// </summary>
            /// <param name="number">Index number 0-99</param>
            /// <param name="minutes">Minutes (0-99)</param>
            /// <param name="seconds">Seconds (0-59)</param>
            /// <param name="frames">Frames (0-74)</param>
            public Index(int number, int minutes, int seconds, int frames)
            {
                _number = number;

                _minutes = minutes;
                _seconds = seconds;
                _frames = frames;
            }

            /// <summary>
            /// Setting or Getting the time stamp in TimeSpan
            /// </summary>
            public TimeSpan Time
            {
                get
                {
                    var milliseconds = (int)Math.Round(_frames * (1000F / 75));
                    return new TimeSpan(0, 0, _minutes, _seconds, milliseconds);
                }

                set
                {
                    Minutes = (value.Hours * 60) + value.Minutes;
                    Seconds = value.Seconds;
                    Frames = (int)Math.Round(value.Milliseconds * 75 / 1000F);
                }
            }
        }

        /// <summary>
        /// This command is used to specify a data/audio file that will be written to the recorder.
        /// </summary>
        public struct AudioFile
        {
            public string Filename { get; set; }

            /// <summary>
            /// BINARY - Intel binary file (least significant byte first)
            /// MOTOROLA - Motorola binary file (most significant byte first)
            /// AIFF - Audio AIFF file
            /// WAVE - Audio WAVE file
            /// MP3 - Audio MP3 file
            /// </summary>
            public FileType Filetype { get; set; }

            public AudioFile(string filename, string filetype)
            {
                Filename = filename;

                switch (filetype.Trim().ToUpper())
                {
                    case "BINARY":
                        Filetype = FileType.BINARY;
                        break;

                    case "MOTOROLA":
                        Filetype = FileType.MOTOROLA;
                        break;

                    case "AIFF":
                        Filetype = FileType.AIFF;
                        break;

                    case "WAVE":
                        Filetype = FileType.WAVE;
                        break;

                    case "MP3":
                        Filetype = FileType.MP3;
                        break;

                    default:
                        Filetype = FileType.BINARY;
                        break;
                }
            }

            public AudioFile(string filename, FileType filetype)
            {
                Filename = filename;
                Filetype = filetype;
            }
        }

        /// <summary>
        /// Track that contains either data or audio. It can contain Indices and comment information.
        /// </summary>
        public struct Track
        {
            #region Private Variables

            // strings that don't belong or were mistyped in the global part of the cue
            #endregion Private Variables

            #region Properties

            /// <summary>
            /// Returns/Sets Index in this track.
            /// </summary>
            /// <param name="indexnumber">Index in the track.</param>
            /// <returns>Index at indexnumber.</returns>
            public Index this[int indexnumber]
            {
                get => Indices[indexnumber];
                set => Indices[indexnumber] = value;
            }

            public string[] Comments { get; set; }

            public AudioFile DataFile { get; set; }

            /// <summary>
            /// Lines in the cue file that don't belong or have other general syntax errors.
            /// </summary>
            public string[] Garbage { get; set; }

            public Index[] Indices { get; set; }

            public string ISRC { get; set; }

            public string Performer { get; set; }

            public Index PostGap { get; set; }

            public Index PreGap { get; set; }

            public string Songwriter { get; set; }

            /// <summary>
            /// If the TITLE command appears before any TRACK commands, then the string will be encoded as the title of the entire disc.
            /// </summary>
            public string Title { get; set; }

            public DataType TrackDataType { get; set; }

            public Flags[] TrackFlags { get; set; }

            public int TrackNumber { get; set; }

            #endregion Properties

            #region Contructors

            public Track(int tracknumber, string datatype)
            {
                TrackNumber = tracknumber;

                switch (datatype.Trim().ToUpper())
                {
                    case "AUDIO":
                        TrackDataType = DataType.AUDIO;
                        break;

                    case "CDG":
                        TrackDataType = DataType.CDG;
                        break;

                    case "MODE1/2048":
                        TrackDataType = DataType.MODE1_2048;
                        break;

                    case "MODE1/2352":
                        TrackDataType = DataType.MODE1_2352;
                        break;

                    case "MODE2/2336":
                        TrackDataType = DataType.MODE2_2336;
                        break;

                    case "MODE2/2352":
                        TrackDataType = DataType.MODE2_2352;
                        break;

                    case "CDI/2336":
                        TrackDataType = DataType.CDI_2336;
                        break;

                    case "CDI/2352":
                        TrackDataType = DataType.CDI_2352;
                        break;

                    default:
                        TrackDataType = DataType.AUDIO;
                        break;
                }

                TrackFlags = new Flags[0];
                Songwriter = string.Empty;
                Title = string.Empty;
                ISRC = string.Empty;
                Performer = string.Empty;
                Indices = new Index[0];
                Garbage = new string[0];
                Comments = new string[0];
                PreGap = new Index(-1, 0, 0, 0);
                PostGap = new Index(-1, 0, 0, 0);
                DataFile = default(AudioFile);
            }

            public Track(int tracknumber, DataType datatype)
            {
                TrackNumber = tracknumber;
                TrackDataType = datatype;

                TrackFlags = new Flags[0];
                Songwriter = string.Empty;
                Title = string.Empty;
                ISRC = string.Empty;
                Performer = string.Empty;
                Indices = new Index[0];
                Garbage = new string[0];
                Comments = new string[0];
                PreGap = new Index(-1, 0, 0, 0);
                PostGap = new Index(-1, 0, 0, 0);
                DataFile = default(AudioFile);
            }

            #endregion Contructors

            #region Methods

            public void AddFlag(Flags flag)
            {
                // if it's not a none tag
                // and if the tags hasn't already been added
                if (flag != Flags.NONE && NewFlag(flag))
                {
                    TrackFlags = (Flags[])CueSheet.ResizeArray(TrackFlags, TrackFlags.Length + 1);
                    TrackFlags[TrackFlags.Length - 1] = flag;
                }
            }

            public void AddFlag(string flag)
            {
                switch (flag.Trim().ToUpper())
                {
                    case "DATA":
                        AddFlag(Flags.DATA);
                        break;

                    case "DCP":
                        AddFlag(Flags.DCP);
                        break;

                    case "4CH":
                        AddFlag(Flags.CH4);
                        break;

                    case "PRE":
                        AddFlag(Flags.PRE);
                        break;

                    case "SCMS":
                        AddFlag(Flags.SCMS);
                        break;

                    default:
                        return;
                }
            }

            public TimeSpan Index00
            {
                get
                {
                    if (Indices.Length < 2)
                    {
                        return TimeSpan.Zero;
                    }
                    return Indices.First().Time;
                }
            }

            public TimeSpan Index01
            {
                get
                {
                    if (Indices.Length < 1)
                    {
                        return TimeSpan.Zero;
                    }
                    return Indices.Last().Time;
                }
            }

            public void AddGarbage(string garbage)
            {
                if (garbage.Trim() != string.Empty)
                {
                    Garbage = (string[])CueSheet.ResizeArray(Garbage, Garbage.Length + 1);
                    Garbage[Garbage.Length - 1] = garbage;
                }
            }

            public void AddComment(string comment)
            {
                if (comment.Trim() != string.Empty)
                {
                    Comments = (string[])CueSheet.ResizeArray(Comments, Comments.Length + 1);
                    Comments[Comments.Length - 1] = comment;
                }
            }

            public void AddIndex(int number, int minutes, int seconds, int frames)
            {
                Indices = (Index[])CueSheet.ResizeArray(Indices, Indices.Length + 1);

                Indices[Indices.Length - 1] = new Index(number, minutes, seconds, frames);
            }

            public void RemoveIndex(int indexIndex)
            {
                for (var i = indexIndex; i < Indices.Length - 1; i++)
                {
                    Indices[i] = Indices[i + 1];
                }
                Indices = (Index[])CueSheet.ResizeArray(Indices, Indices.Length - 1);
            }

            /// <summary>
            /// Checks if the flag is indeed new in this track.
            /// </summary>
            /// <param name="newFlag">The new flag to be added to the track.</param>
            /// <returns>True if this flag doesn't already exist.</returns>
            private bool NewFlag(Flags newFlag)
            {
                return TrackFlags.All(flag => flag != newFlag);
            }

            public override string ToString()
            {
                var output = new StringBuilder();

                // write file
                if (DataFile.Filename != null && DataFile.Filename.Trim() != string.Empty)
                {
                    output.Append("FILE \"" + DataFile.Filename.Trim() + "\" " + DataFile.Filetype.ToString() + Environment.NewLine);
                }

                output.Append("  TRACK " + TrackNumber.ToString().PadLeft(2, '0') + " " + TrackDataType.ToString().Replace('_', '/'));

                // write comments
                foreach (var comment in Comments)
                {
                    output.Append(Environment.NewLine + "    REM " + comment);
                }

                if (Performer.Trim() != string.Empty)
                {
                    output.Append(Environment.NewLine + "    PERFORMER \"" + Performer + "\"");
                }

                if (Songwriter.Trim() != string.Empty)
                {
                    output.Append(Environment.NewLine + "    SONGWRITER \"" + Songwriter + "\"");
                }

                if (Title.Trim() != string.Empty)
                {
                    output.Append(Environment.NewLine + "    TITLE \"" + Title + "\"");
                }

                // write flags
                if (TrackFlags.Length > 0)
                {
                    output.Append(Environment.NewLine + "    FLAGS");
                }

                foreach (var flag in TrackFlags)
                {
                    output.Append(" " + flag.ToString().Replace("CH4", "4CH"));
                }

                // write isrc
                if (ISRC.Trim() != string.Empty)
                {
                    output.Append(Environment.NewLine + "    ISRC " + ISRC.Trim());
                }

                // write pregap
                if (PreGap.Number != -1)
                {
                    output.Append(Environment.NewLine + "    PREGAP " + PreGap.Minutes.ToString().PadLeft(2, '0') + ":" + PreGap.Seconds.ToString().PadLeft(2, '0') + ":" + PreGap.Frames.ToString().PadLeft(2, '0'));
                }

                // write Indices
                for (var j = 0; j < Indices.Length; j++)
                {
                    output.Append(Environment.NewLine + "    INDEX " + this[j].Number.ToString().PadLeft(2, '0') + " " + this[j].Minutes.ToString().PadLeft(2, '0') + ":" + this[j].Seconds.ToString().PadLeft(2, '0') + ":" + this[j].Frames.ToString().PadLeft(2, '0'));
                }

                // write postgap
                if (PostGap.Number != -1)
                {
                    output.Append(Environment.NewLine + "    POSTGAP " + PostGap.Minutes.ToString().PadLeft(2, '0') + ":" + PostGap.Seconds.ToString().PadLeft(2, '0') + ":" + PostGap.Frames.ToString().PadLeft(2, '0'));
                }

                return output.ToString();
            }

            #endregion Methods
        }
    }
}