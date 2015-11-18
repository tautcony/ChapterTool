﻿// ****************************************************************************
//
// Copyright (C) 2014-2015 TautCony (TautCony@vcb-s.com)
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// ****************************************************************************
using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Linq;
using System.Drawing;
using ChapterTool.Util;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ChapterTool.Forms
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        public Form1(string args)
        {
            InitializeComponent();
            _paths[0] = args;
            CTLogger.Log($"+从运行参数中载入文件:{args}");
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            InitialLog();
            Point saved = ConvertMethod.String2Point(RegistryStorage.Load(@"Software\ChapterTool", "location"));
            if (saved != new Point(-32000, -32000))
            {
                Location = saved;
                CTLogger.Log($"成功载入保存的窗体位置{saved}");
            }
            LoadLang();
            SetDefault();
            ConvertMethod.LoadColor(this);
            MoreModeShow = false;
            Size = new Size(Size.Width, Size.Height - 80);
            savingType.SelectedIndex = 0;
            btnTrans.Text = ((Environment.TickCount % 2 == 0) ? "↺" : "↻");
            folderBrowserDialog1.SelectedPath = RegistryStorage.Load();
            if (!string.IsNullOrEmpty(_paths[0]))
            {
                Loadfile();
                UpdataGridView();
                RegistryStorage.Save("你好呀，找到这里来干嘛呀", @"Software\ChapterTool", string.Empty);
            }
            var countS = RegistryStorage.Load(@"Software\ChapterTool\Statistics", @"Count");
            int count = string.IsNullOrEmpty(countS) ? 0: int.Parse(countS);
            RegistryStorage.Save((++count).ToString(), @"Software\ChapterTool\Statistics", @"Count");
        }

        static void InitialLog()
        {
            if (Environment.UserName.ToLowerInvariant().IndexOf("yzy", StringComparison.Ordinal) > 0) { CTLogger.Log("武总好~"); }
            else { CTLogger.Log(Environment.UserName + "，你好呀"); }
            CTLogger.Log(Environment.OSVersion.ToString());
            if (Environment.GetLogicalDrives().Length > 10) { CTLogger.Log("硬盘壕，给我块硬盘呗~"); }
            using (var registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
            {
                var processor = string.Empty;
                //\HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0
                if (registryKey != null)
                {
                    processor = (string)registryKey.GetValue("ProcessorNameString");
                }
                registryKey?.Close();
                CTLogger.Log(processor);
            }
            Screen.AllScreens.ToList().ForEach(item => CTLogger.Log( $"{item.DeviceName} 分辨率：{item.Bounds.Width}*{item.Bounds.Height}"));
        }


        void LoadLang()
        {
            xmlLang.Items.Add("----常用----");
            xmlLang.Items.Add("und (Undetermined)");
            xmlLang.Items.Add("eng (English)");
            xmlLang.Items.Add("jpn (Japanese)");
            xmlLang.Items.Add("chi (Chinese)");
            xmlLang.Items.Add("----全部----");
            LanguageSelectionContainer.Languages.ToList().ForEach(item => xmlLang.Items.Add($"{item.Value} ({item.Key})"));
        }


        ChapterInfo _info;

        string SchapterFitter => !File.Exists("mkvextract.exe") ? "章节文件(*.txt,*.xml,*.mpls,*.ifo)|*.txt;*.xml;*.mpls;*.ifo" : "所有支持的类型(*.txt,*.xml,*.mpls,*.ifo,*.mkv,*.mka)|*.txt;*.xml;*.mpls;*.ifo;*.mkv;*.mka|章节文件(*.txt,*.xml,*.mpls,*.ifo)|*.txt;*.xml;*.mpls;*.ifo|Matroska文件(*.mkv,*.mka)|*.mkv;*.mka";
        readonly string SnotLoaded = "尚未载入文件";
        readonly string Ssuccess = "载入完成 (≧▽≦)";
        readonly string Swhatsthis2 = "当前片段并没有章节 (¬_¬)";


        void SetDefault()
        {
            cbMore.CheckState = CheckState.Unchecked;
            MoreModeShow = false;
            comboBox2.Enabled = comboBox2.Visible = false;

            comboBox1.SelectedIndex = -1;
            btnSave.Enabled = btnSave.Visible = true;

            progressBar1.Visible = true;
            cbMore.Enabled = true;
            cbMul1k1.Enabled = true;


            _rawMpls  = null;
            _xmlGroup = null;
            xmlLang.SelectedIndex = 2;
        }

        readonly Regex _rLineOne    = new Regex(@"CHAPTER\d+=\d+:\d+:\d+\.\d+");
        readonly Regex _rLineTwo    = new Regex(@"CHAPTER\d+NAME=(?<chapterName>.*)");

        string[] _paths = new string[20];

        void Form1_DragDrop(object sender,  DragEventArgs e)
        {
            _paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (!IsPathValid) { return; }
            CTLogger.Log("+从窗口拖拽中载入文件:" + _paths?[0]);
            comboBox2.Items.Clear();
            Loadfile();
            UpdataGridView();
        }
        void Form1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }


        int[] _poi = { 0, 10 };

        void progressBar1_Click(object sender, EventArgs e)
        {
            ++_poi[0];
            CTLogger.Log($"点击了 {_poi[0]} 次进度条");
            if (_poi[0] >= _poi[1])
            {
                Form2 version = new Form2();
                CTLogger.Log("打开了关于界面");
                version.Show();
                _poi[0] = 0;
                _poi[1] += 10;
                CTLogger.Log("进度条点击计数清零");
            }
            if (_poi[0] < 3 && _poi[1] == 10)
            {
                MessageBox.Show(@"Something happened", @"Something happened");
            }
        }

        readonly Regex _rFileType = new Regex(@".(txt|xml|mpls|ifo|mkv|mka)$");

        bool IsPathValid
        {
            get
            {
                if (string.IsNullOrEmpty(_paths[0]))
                {
                    Tips.Text = @"文件还没载入呢";
                    return false;
                }
                if (_rFileType.IsMatch(_paths[0].ToLowerInvariant())) return true;
                Tips.Text = @"这个文件我不认识啊 _ (:3」∠)_";
                CTLogger.Log("文件格式非法");
                _paths[0] = string.Empty;
                label1.Text = SnotLoaded;
                return false;
            }
        }


        void Loadfile()
        {
            if (!IsPathValid) { return; }
            label1.Text = (_paths[0].Length > 55) ? _paths[0].Substring(0, 40) + "……" + _paths[0].Substring(_paths[0].Length - 15, 15) : _paths[0];

            SetDefault();
            Cursor = Cursors.AppStarting;
            try
            {
                switch (_rFileType.Match(_paths[0].ToLowerInvariant()).Value)
                {
                    case ".mpls": LoadMpls(); break;
                    case ".xml":   LoadXml(); break;
                    case ".txt":   LoadOgm(); break;
                    case ".ifo":   LoadIfo(); break;
                    case ".mkv":
                    case ".mka": if (File.Exists("mkvextract.exe")) { LoadMatroska(); } break;
                }
                if (!string.IsNullOrEmpty(_chapterNameTemplate)) { UpdataInfo(_chapterNameTemplate); }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                CTLogger.Log("ERROR: " + ex.Message);
                label1.Text = SnotLoaded;
            }
            Cursor = Cursors.Default;

        }

        List<ChapterInfo> _rawIfo;


        void LoadIfo()
        {
            _rawIfo = new IfoData().GetStreams(_paths[0]);
            if (Math.Abs(_rawIfo[0].FramesPerSecond - 25) > 1e-5)
            {
                IfoMul1K1();
            }
            comboBox2.Items.Clear();
            comboBox2.Enabled = comboBox2.Visible = (_rawIfo.Count >= 1);
            foreach (var item in _rawIfo.Where(item => comboBox2.Enabled && item != null))
            {
                comboBox2.Items.Add($"{item.SourceName}__{item.Chapters.Count}");
                CTLogger.Log($" |+{item.SourceName}");
                CTLogger.Log($"  |+包含 {item.Chapters.Count} 个时间戳");
            }
            int i = 0;
            foreach (var item in _rawIfo)
            {
                if (item != null)
                {
                    _info = item;
                    GenerateChapterInfo(i, true);
                    comboBox2.SelectedIndex = i;
                    break;
                }
                ++i;
            }
            Tips.Text = (comboBox2.SelectedIndex == -1) ? Swhatsthis2 : Ssuccess;
        }

        void IfoMul1K1()
        {
            foreach (var item in _rawIfo.Where(item => item != null))
            {
                item.Chapters.ForEach(item2 => item2.Time = ConvertMethod.Pts2Time((int)((decimal)item2.Time.TotalSeconds * 1.001M * 45000M)));
            }
        }



        void LoadOgm()
        {
            byte[] buffer = File.ReadAllBytes(_paths[0]);
            GenerateChapterInfo(ConvertMethod.GetUTF8String(buffer));
            progressBar1.Value = 33;
            Tips.Text = Ssuccess;
        }

        void btnLoad_Click(object sender, EventArgs e)                  //载入键
        {
            openFileDialog1.Filter = SchapterFitter;
            try
            {
                if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
                _paths[0] = openFileDialog1.FileName;
                CTLogger.Log("+从载入键中载入文件:" + _paths[0]);
                comboBox2.Items.Clear();
                Loadfile();
                UpdataGridView();
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Error opening file {_paths[0]}: {exception.Message}{Environment.NewLine}", @"ChapterTool Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                CTLogger.Log($"Error opening file {_paths[0]}: {exception.Message}");
            }
        }

        void btnSave_Click(object sender, EventArgs e) { SaveFile(); }  //输出保存键

        string _customSavingPath = string.Empty;

        private void btnSave_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button != MouseButtons.Right || folderBrowserDialog1.ShowDialog() != DialogResult.OK) return;
                _customSavingPath = folderBrowserDialog1.SelectedPath;
                RegistryStorage.Save(_customSavingPath);
                CTLogger.Log("设置保存路径为:" + _customSavingPath);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Error opening path {_customSavingPath}: {exception.Message}{Environment.NewLine}", @"ChapterTool Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                CTLogger.Log($"Error opening path {_customSavingPath}: {exception.Message}");
            }
        }

        readonly Regex _rLang = new Regex(@"\((?<lang>.+)\)");

        void SaveFile()
        {
            if (!IsPathValid) { return; }


            string pn = _paths[0].Substring(0, _paths[0].LastIndexOf(".", StringComparison.Ordinal));
            //modify for custom saving path
            int slashPosition = _paths[0].LastIndexOf(@"\", StringComparison.Ordinal);
            if (!string.IsNullOrEmpty(_customSavingPath))
                pn = _customSavingPath + _paths[0].Substring(slashPosition, _paths[0].LastIndexOf(".", StringComparison.Ordinal) - slashPosition);
            StringBuilder savePath = new StringBuilder(pn);

            if (_paths[0].ToLowerInvariant().EndsWith(".mpls") && !combineToolStripMenuItem.Checked)
                savePath.Append("__" + _rawMpls.ChapterClips[MplsFileSeletIndex].Name);
            if (_paths[0].ToLowerInvariant().EndsWith(".ifo"))
                savePath.Append("__" + _rawIfo[MplsFileSeletIndex].SourceName);

            switch (savingType.SelectedIndex)
            {
                case 0://TXT
                    while (File.Exists(savePath + ".txt")) { savePath.Append("_"); }
                    savePath.Append(".txt");
                    _info.SaveText(savePath.ToString(), cbAutoGenName.Checked);
                    break;
                case 1://XML
                    while (File.Exists(savePath + ".xml")) { savePath.Append("_"); }
                    savePath.Append(".xml");
                    string key = _rLang.Match(xmlLang.Items[xmlLang.SelectedIndex].ToString()).Groups["lang"].ToString();
                    _info.SaveXml(savePath.ToString(),string.IsNullOrEmpty(key)? "": LanguageSelectionContainer.Languages[key]);
                    break;
                case 2://QPF
                    while (File.Exists(savePath + ".qpf")) { savePath.Append("_"); }
                    savePath.Append(".qpf");
                    _info.SaveQpfile(savePath.ToString());
                    break;
            }
            progressBar1.Value = 100;
            Tips.Text = @"保存成功";
        }

        private void savingType_SelectedIndexChanged(object sender, EventArgs e)
        {
            xmlLang.Enabled = (savingType.SelectedIndex == 1);
        }

        void refresh_Click(object sender, EventArgs e) { UpdataGridView(); }


        TimeSpan OffsetCal_new(string line)//获取第一行的时间
        {
            if (_rLineOne.IsMatch(line))
            {
                return ConvertMethod.String2Time(ConvertMethod.RTimeFormat.Match(line).Value);
            }
            else
            {
                CTLogger.Log($"ERROR: {line} <-该行与时间行格式不匹配");
                return TimeSpan.Zero;
            }
        }
        Chapter WriteToChapterInfo(string line, string line2, int order,TimeSpan iniTime)
        {
            Chapter temp = new Chapter();
            if (_rLineTwo.IsMatch(line2))                     //章节标题行
            {
                switch (!cbAutoGenName.Checked)
                {
                    case true:
                        temp.Name = _rLineTwo.Match(line2).Groups["chapterName"].Value; break;
                    case false:
                        temp.Name = $"Chapter {order:D2}"; break;
                }
            }
            if (_rLineOne.IsMatch(line))
            {
                temp.Time = ConvertMethod.String2Time(ConvertMethod.RTimeFormat.Match(line).Value) - iniTime;
            }
            temp.Number = order;
            return temp;
        }
        #region geneRateCI
        void GenerateChapterInfo(string text)
        {
            _info = new ChapterInfo
            {
                SourceHash = IfoData.ComputeMD5Sum(_paths[0]),
                SourceType = "OGM"
            };
            List<string>.Enumerator ogmData = text.Split('\n').ToList().GetEnumerator();
            do
            {
                if (!ogmData.MoveNext()) { return; }
            } while ((string.IsNullOrEmpty(ogmData.Current) || ogmData.Current == "\r"));
            TimeSpan iniTime =  OffsetCal_new(ogmData.Current);
            int order = 1 + (int)numericUpDown1.Value;
            do
            {
                string buffer1 = ogmData.Current;
                ogmData.MoveNext();
                string buffer2 = ogmData.Current;
                if (string.IsNullOrEmpty(buffer1) ||
                    string.IsNullOrEmpty(buffer2) ||
                    buffer1 == "\r" || buffer2 == "\r" ) { break; }
                if (_rLineOne.IsMatch(buffer1) && _rLineTwo.IsMatch(buffer2))
                {
                    _info.Chapters.Add(WriteToChapterInfo(buffer1, buffer2, order++, iniTime));
                }
            } while (ogmData.MoveNext());
            if (_info.Chapters.Count>1)
            {
                _info.Duration = _info.Chapters[_info.Chapters.Count - 1].Time;
            }
            ogmData.Dispose();
        }

        void GenerateChapterInfo(int index)
        {
            Clip mplsClip = _rawMpls.ChapterClips[index];
            _info = new ChapterInfo
            {
                SourceHash = IfoData.ComputeMD5Sum(_paths[0]),
                Duration = ConvertMethod.Pts2Time(mplsClip.TimeOut - mplsClip.TimeIn),
                SourceType = "MPLS",
                FramesPerSecond = (double) _frameRate[_rawMpls.ChapterClips[index].Fps]
            };
            List<int> current;
            if (combineToolStripMenuItem.Checked)
            {
                current = _rawMpls.EntireTimeStamp;
                _info.Title = "FULL Chapter";
            }
            else
            {
                current = mplsClip.TimeStamp;
                _info.Title = _rawMpls.ChapterClips[index].Name;
            }
            if (current.Count < 2)
            {
                Tips.Text = Swhatsthis2;
                return;
            }
            else
            {
                Tips.Text = Ssuccess;
            }
            int offset = current[0];

            int defaultOrder = 1;
            foreach (var item in current)
            {
                Chapter temp = new Chapter
                {
                    Time = ConvertMethod.Pts2Time(item - offset),
                    Name = $"Chapter {defaultOrder:D2}",
                    Number = defaultOrder++
                };
                _info.Chapters.Add(temp);
            }
            if (!string.IsNullOrEmpty(_chapterNameTemplate)) { UpdataInfo(_chapterNameTemplate); }
        }


        List<ChapterInfo> _xmlGroup;



        void GenerateChapterInfo(XmlDocument doc)
        {
            _xmlGroup = ConvertMethod.PraseXML(doc);
            comboBox2.Enabled = comboBox2.Visible = (_xmlGroup.Count >= 1);
            if (comboBox2.Enabled)
            {
                comboBox2.Items.Clear();
                int i = 1;
                foreach (var item in _xmlGroup)
                {
                    string name = $"Edition {i++:D2}";
                    comboBox2.Items.Add(name);
                    CTLogger.Log($" |+{name}");
                    CTLogger.Log($"  |+包含 {item.Chapters.Count} 个时间戳");
                }
            }
            _info = _xmlGroup[0];
            comboBox2.SelectedIndex = MplsFileSeletIndex;
        }

        void GenerateChapterInfo(int index, bool dvd)
        {
            if (_rawIfo[index] != null && dvd)
            {
                _info = _rawIfo[index];
            }
            else
            {
                Tips.Text = Swhatsthis2;
            }
        }
        #endregion

        #region updataInfo
        void UpdataInfo(TimeSpan shift)
        {
            if (!IsPathValid) { return; }
            _info.Chapters.ForEach(item => item.Time -= shift);
        }
        void UpdataInfo(int shift)
        {
            if (!IsPathValid) { return; }
            int i = 1;
            _info.Chapters.ForEach(item => item.Number = i++ + shift);
        }
        void UpdataInfo(string chapterName)
        {
            if (!IsPathValid) { return; }
            var cn = chapterName.Split('\n').ToList().GetEnumerator();
            _info.Chapters.ForEach(item =>
            {
                if (cn.MoveNext())
                {
                    item.Name = cn.Current;
                }
            });
            cn.Dispose();
        }
        void UpdataInfo(decimal coefficient)
        {
            if (!IsPathValid) { return; }
            _info.Chapters.ForEach(item => item.Time = ConvertMethod.Pts2Time((int)((decimal)item.Time.TotalSeconds * coefficient * 45000M)));
        }
        #endregion



        void UpdataGridView(int fpsIndex = 0)
        {
            if (!IsPathValid || _info == null) { return; }
            switch (_info.SourceType)
            {
                case "DVD":
                    GetFramInfo(ConvertMethod.ConvertFr2Index(_info.FramesPerSecond));
                    comboBox1.Enabled = false;
                    break;
                case "MPLS":
                    int index = _rawMpls.ChapterClips[MplsFileSeletIndex].Fps;
                    GetFramInfo(index);
                    comboBox1.Enabled = false;
                    break;
                default:
                    GetFramInfo(fpsIndex);
                    _info.FramesPerSecond = (double)_frameRate[comboBox1.SelectedIndex];
                    comboBox1.Enabled = true;
                    break;
            }

            bool clearOrNot = _info.Chapters.Count != dataGridView1.Rows.Count;
            if (clearOrNot)
            {
                dataGridView1.Rows.Clear();
            }

            for (var i = 0; i < _info.Chapters.Count; i++)
            {
                if (clearOrNot) { dataGridView1.Rows.Add(); }
                dataGridView1.Rows[i].DefaultCellStyle.BackColor = ((i % 2 == 0) ? Color.FromArgb(0x92,0xaa,0xf3) : Color.FromArgb(0xf3, 0xf7, 0xf7));
                AddRow(_info.Chapters[i], i);
            }
            progressBar1.Value = (dataGridView1.RowCount > 1) ? 66 : 33;
        }


        void AddRow(Chapter item,int index)
        {
            dataGridView1.Rows[index].Tag = item;
            dataGridView1.Rows[index].Cells[0].Value = item.Number.ToString("00");
            dataGridView1.Rows[index].Cells[1].Value = ConvertMethod.time2string(item.Time + _info.Offset);
            dataGridView1.Rows[index].Cells[2].Value = cbAutoGenName.Checked ? $"Chapter {(index + 1):D2}" : item.Name;
            dataGridView1.Rows[index].Cells[3].Value = item.FramsInfo;
        }

        /// FPS Cal Part /////////////////////

        decimal CostumeAccuracy
        {
            get
            {
                decimal[] accuracy = { 0.01M, 0.05M, 0.10M, 0.15M, 0.20M, 0.25M, 0.30M };
                return accuracy[int.Parse(toolStripMenuItem1.DropDownItems.OfType<ToolStripMenuItem>().First(item => item.Checked).Tag.ToString())];
            }
        }

        void GetFramInfo(int index = 0)
        {
            var settingAccuracy = CostumeAccuracy;
            index = index == 0 ? GetAutofps(settingAccuracy) : index;
            comboBox1.SelectedIndex = index - 1;

            foreach (var item in _info.Chapters)
            {
                var frams  = ((decimal)item.Time.TotalMilliseconds * _frameRate[index] / 1000M);
                var answer = cbRound.Checked ? Math.Round(frams, MidpointRounding.AwayFromZero) : frams;
                bool accuracy  = (Math.Abs(frams - answer) < settingAccuracy);
                item.FramsInfo = $"{answer}{(accuracy ? " K" : " *")}";
            }
        }
        int GetAutofps(decimal accuracy)
        {
            CTLogger.Log($"|+自动帧率识别开始，允许误差为：{accuracy}");
            List<int> result = _frameRate.Select(fps => _info.Chapters.Sum(item => GetAccuracy(item.Time, fps))).ToList();
            result.ForEach(count => CTLogger.Log($" | {count:D2} 个精确点"));
            result[0] = 0;
            int autofpsCode = result.IndexOf(result.Max());
            CTLogger.Log($" |自动帧率识别结果为 {_frameRate[autofpsCode]:F4} fps");
            return autofpsCode == 0 ? 1 : autofpsCode;
        }

        int GetAccuracy(TimeSpan time, decimal fps)//framCal
        {
            var frams  = ((decimal)time.TotalMilliseconds * fps / 1000M);
            var answer = cbRound.Checked ? Math.Round(frams, MidpointRounding.AwayFromZero) : frams;
            return (Math.Abs(frams - answer) < CostumeAccuracy) ? 1 : 0;
        }

        private void comboBox1_SelectionChangeCommitted(object sender, EventArgs e)
        {
            UpdataGridView(comboBox1.SelectedIndex + 1);
        }

        private void Accuracy_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            toolStripMenuItem1.DropDownItems.OfType<ToolStripMenuItem>().ToList().ForEach(item => item.Checked = false);
            ((ToolStripMenuItem) e.ClickedItem).Checked = true;
        }

        void cbShift_CheckedChanged(object sender, EventArgs e)
        {
            if (!IsPathValid) { return; }
            _info.Offset = cbShift.Checked ? GetOffsetFromMaskedTextBox() : TimeSpan.Zero;
            UpdataGridView();
        }

        void maskedTextBox1_MaskInputRejected(object sender, MaskInputRejectedEventArgs e) { Tips.Text = @"位移时间不科学的样子"; }

        TimeSpan GetOffsetFromMaskedTextBox()
        {
            if (ConvertMethod.RTimeFormat.IsMatch(maskedTextBox1.Text))
            {
                return ConvertMethod.String2Time(maskedTextBox1.Text);
            }
            Tips.Text = @"位移时间不科学的样子";
            return TimeSpan.Zero;
        }


        #region form resize support
        bool MoreModeShow
        {
            set
            {
                label2.Visible         = value;
                savingType.Visible     = value;
                cbAutoGenName.Visible  = value;
                label3.Visible         = value;
                numericUpDown1.Visible = value;
                cbMul1k1.Visible       = value;
                cbChapterName.Visible  = value;
                cbShift.Visible        = value;
                maskedTextBox1.Visible = value;
                btnLog.Visible         = value;
                label4.Visible         = value;
                xmlLang.Visible        = value;
            }
        }
        void Form1_Resize()
        {
            if (cbMore.Checked)
            {
                Enumerable.Range(Size.Height, 80).ToList().ForEach(x => Size = new Size(Size.Width, x));
            }
            else
            {
                Enumerable.Range(Size.Height - 80, 80).Reverse().ToList().ForEach(x => Size = new Size(Size.Width, x));
            }
            MoreModeShow = cbMore.Checked;
        }
        void cbMore_CheckedChanged(object sender, EventArgs e)
        {
            Form1_Resize();
            cbMore.Text = cbMore.Checked ? "∧" : "∨";
        }
        #endregion

        string LoadChapterName()
        {
            openFileDialog1.Filter = @"文本文件(*.txt)|*.txt|所有文件(*.*)|*.*";
            try
            {
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    string chapterPath = openFileDialog1.FileName;
                    CTLogger.Log("+载入自定义章节名模板：" + chapterPath);
                    byte[] buffer = File.ReadAllBytes(chapterPath);
                    return ConvertMethod.GetUTF8String(buffer);
                }
                cbChapterName.CheckState = CheckState.Unchecked;
                return string.Empty;
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Error opening file {_paths[0]}: {exception.Message}{Environment.NewLine}", @"ChapterTool Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                CTLogger.Log($"Error opening file {_paths[0]}: {exception.Message}");
                return string.Empty;
            }
        }

        string _chapterNameTemplate;

       private void cbChapterName_CheckedChanged(object sender, EventArgs e)       //载入客章节模板或清除
       {
            if(cbChapterName.Checked)
            {
                _chapterNameTemplate = LoadChapterName();
                UpdataInfo(_chapterNameTemplate);
            }
            else
            {
                _chapterNameTemplate = string.Empty;
            }
            UpdataGridView();
        }

        /////////////////XML support
        void LoadXml()
        {
            var doc = new XmlDocument();
            doc.Load(_paths[0]);
            GenerateChapterInfo(doc);
        }

        /////////////////mpls support


        readonly List<decimal> _frameRate = new List<decimal> { 0M, 24000M / 1001, 24000M / 1000,
                                                          25000M / 1000, 30000M / 1001,
                                                          50000M / 1000, 60000M / 1001 };

        mplsData _rawMpls;

        int MplsFileSeletIndex => comboBox2.SelectedIndex == -1 ? 0 : comboBox2.SelectedIndex;

        void LoadMpls()
        {
            _rawMpls = new mplsData(_paths[0]);
            CTLogger.Log("+成功载入MPLS格式章节文件");
            CTLogger.Log($"|+MPLS中共有 {_rawMpls.ChapterClips.Count} 个m2ts片段");

            comboBox2.Enabled = comboBox2.Visible = (_rawMpls.ChapterClips.Count >= 1);
            if (comboBox2.Enabled)
            {
                comboBox2.Items.Clear();
                _rawMpls.ChapterClips.ForEach(item =>
                {
                    comboBox2.Items.Add($"{item.Name.Replace("M2TS", ".m2ts")}__{item.TimeStamp.Count}");
                    CTLogger.Log($" |+{item.Name}");
                    CTLogger.Log($"  |+包含 {item.TimeStamp.Count} 个时间戳");
                });
            }
            comboBox2.SelectedIndex = MplsFileSeletIndex;
            GenerateChapterInfo(MplsFileSeletIndex);
        }


        void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_rawMpls != null)
            {
                GenerateChapterInfo(comboBox2.SelectedIndex);
                goto updata;
            }
            if (_xmlGroup != null)
            {
                _info = _xmlGroup[comboBox2.SelectedIndex];
                goto updata;
            }
            GenerateChapterInfo(comboBox2.SelectedIndex, true);
            updata:
            UpdataGridView();
        }


        private void combineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_rawMpls == null) { return; }
            combineToolStripMenuItem.Checked = !combineToolStripMenuItem.Checked;
            GenerateChapterInfo(comboBox2.SelectedIndex);
            UpdataGridView();
        }

        /// <summary>
        /// generate the chapter info from a matroska file
        /// </summary>
        void LoadMatroska()
        {
            var matroska = new MatroskaInfo(_paths[0]);
            GenerateChapterInfo(matroska.Result);
        }

        #region color support
        Form3 _fcolor;

        private void Color_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (_fcolor == null)
            {
                _fcolor = new Form3(this);
            }
            CTLogger.Log("颜色设置窗口被打开");
            _fcolor.Show();
            _fcolor.Focus();
            _fcolor.Select();
        }
        public List<Color> CurrentColor
        {
            get
            {
                List<Color> temp = new List<Color>
                {
                    BackChange,
                    TextBack,
                    MouseOverColor,
                    MouseDownColor,
                    BordBackColor,
                    TextFrontColor
                };
                return temp;
            }
        }
        public Color BackChange
        {
            set
            {
                BackColor                                    = value;
                cbMore.BackColor                             = value;
            }
            get
            {
                return BackColor;
            }
        }
        public Color TextBack
        {
            set
            {
                dataGridView1.BackgroundColor                = value;
                numericUpDown1.BackColor                     = value;
                maskedTextBox1.BackColor                     = value;
                comboBox1.BackColor                          = value;
                comboBox2.BackColor                          = value;
                xmlLang.BackColor                            = value;
                savingType.BackColor                         = value;
            }
            get
            {
                return dataGridView1.BackgroundColor;
            }
        }
        public Color MouseOverColor
        {
            set
            {
                btnLoad.FlatAppearance.MouseOverBackColor    = value;
                btnSave.FlatAppearance.MouseOverBackColor    = value;
                btnTrans.FlatAppearance.MouseOverBackColor   = value;
                btnLog.FlatAppearance.MouseOverBackColor     = value;
                btnPreview.FlatAppearance.MouseOverBackColor = value;
                cbMore.FlatAppearance.MouseOverBackColor     = value;
            }
            get
            {
                return btnLoad.FlatAppearance.MouseOverBackColor;
            }
        }
        public Color MouseDownColor
        {
            set
            {
                btnLoad.FlatAppearance.MouseDownBackColor    = value;
                btnSave.FlatAppearance.MouseDownBackColor    = value;
                btnTrans.FlatAppearance.MouseDownBackColor   = value;
                btnLog.FlatAppearance.MouseDownBackColor     = value;
                btnPreview.FlatAppearance.MouseDownBackColor = value;
                cbMore.FlatAppearance.MouseDownBackColor     = value;
            }
            get
            {
                return btnLoad.FlatAppearance.MouseDownBackColor;
            }

        }
        public Color BordBackColor
        {
            set
            {
                btnLoad.FlatAppearance.BorderColor           = value;
                btnSave.FlatAppearance.BorderColor           = value;
                btnTrans.FlatAppearance.BorderColor          = value;
                btnLog.FlatAppearance.BorderColor            = value;
                btnPreview.FlatAppearance.BorderColor        = value;
                cbMore.FlatAppearance.BorderColor            = value;
                dataGridView1.GridColor                      = value;
            }
            get
            {
                return btnLoad.FlatAppearance.BorderColor;
            }
        }
        public Color TextFrontColor
        {
            set
            {
                ForeColor                                    = value;
                numericUpDown1.ForeColor                     = value;
                maskedTextBox1.ForeColor                     = value;
                cbMore.ForeColor                             = value;
                comboBox1.ForeColor                          = value;
                comboBox2.ForeColor                          = value;
                xmlLang.ForeColor                            = value;
                savingType.ForeColor                         = value;
                dataGridView1.ForeColor                      = value;
            }
            get
            {
                return ForeColor;
            }
        }
        #endregion

        #region tips support
        private void label1_MouseEnter(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_paths[0]))
            {
                toolTip1.Show(_paths[0], label1);
            }
        }
        private void btnSave_MouseEnter(object sender, EventArgs e)
        {
            const string sFakeChapter1 = "本片段时长为";
            string sFakeChapter2 = $"但是第二个章节点{Environment.NewLine}离视频结尾太近了呢，应该没有用处吧 (-｡-;)";
            string sFakeChapter3 = $"虽然只有两个章节点{Environment.NewLine}应该还是能安心的呢 (～￣▽￣)→))*￣▽￣*)o";
            if (!IsPathValid || !_paths[0].ToLowerInvariant().EndsWith(".mpls")) return;
            int index = MplsFileSeletIndex;
            if (_rawMpls.ChapterClips[index].TimeStamp.Count != 2) return;
            Clip streamClip = _rawMpls.ChapterClips[index];
            string lastTime = ConvertMethod.time2string(streamClip.TimeOut - streamClip.TimeIn);
            if (((streamClip.TimeOut - streamClip.TimeIn) - (streamClip.TimeStamp[1] - streamClip.TimeStamp[0])) <= 5 * 45000)
            {
                toolTip1.Show($"{sFakeChapter1}: {lastTime}，{sFakeChapter2}", btnSave);
            }
            else
            {
                toolTip1.Show($"{sFakeChapter1}: {lastTime}，{sFakeChapter3}", btnSave);
            }
        }

        private void comboBox2_MouseEnter(object sender, EventArgs e)
        {
            toolTip1.Show((comboBox2.Items.Count > 20) ? "不用看了，这是播放菜单的mpls" : comboBox2.Items.Count.ToString(), comboBox2);
        }
        private void cbMul1k1_MouseEnter(object sender, EventArgs e)
        {
            toolTip1.Show("用于DVD Decrypter提取的Chapter", cbMul1k1);
        }
        private void ToolTipRemoveAll(object sender, EventArgs e) { toolTip1.RemoveAll(); }
        #endregion

        #region closing animation support
        public int SystemVersion => Environment.OSVersion.Version.Major;
        //Windows95/98/Me	     	 4
        //Windows2000/XP/2003        5
        //WindowsVista/7/8/8.1/10 	 6

        static void FormMove(int forward,ref Point p)
        {
            switch (forward)
            {
                case 1: ++p.X; break;
                case 2: --p.X; break;
                case 3: ++p.Y; break;
                case 4: --p.Y; break;
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            RegistryStorage.Save(Location.ToString(), @"Software\ChapterTool", "Location");
            if (_poi[0] <= 0 || _poi[0] >= 3 || _poi[1] != 10) return;
            Point origin   = Location;
            Random forward = new Random();
            int forward2   = forward.Next(1, 5);
            if (forward2 % 2 == 0 || SystemVersion == 5)
            {
                foreach (var i in Enumerable.Range(0,50))
                {
                    FormMove(forward.Next(1, 5), ref origin);
                    System.Threading.Thread.Sleep(4);
                    Location = origin;
                }
                return;
            }
            while (Opacity > 0)
            {
                Opacity -= 0.02;
                FormMove(forward2, ref origin);
                System.Threading.Thread.Sleep(4);
                Location = origin;
            }
        }
        #endregion

        FormLog _logForm;
        private void btnLog_Click(object sender, EventArgs e)
        {
            if (_logForm == null)
            {
                _logForm = new FormLog();
            }
            _logForm.Show();
            _logForm.Focus();
            _logForm.Select();
        }
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            UpdataInfo((int)numericUpDown1.Value);
            UpdataGridView();
        }
        private void cbMul1k1_CheckedChanged(object sender, EventArgs e)
        {
            if (_info == null || _info.SourceType == "DVD") return;
            if (cbMul1k1.Checked)
            {
                UpdataInfo(1.001M);
            }
            else
            {
                UpdataInfo(1 / 1.001M);
            }
            UpdataGridView();
        }
        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            CTLogger.Log($"+更名: {_info.Chapters[e.RowIndex].Name} -> {dataGridView1.Rows[e.RowIndex].Cells[2].Value}");
            _info.Chapters[e.RowIndex].Name = dataGridView1.Rows[e.RowIndex].Cells[2].Value.ToString();
        }
        private void dataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            foreach (DataGridViewRow item in dataGridView1.SelectedRows)
            {
                _info.Chapters.Remove((Chapter)item.Tag);
            }
            UpdataInfo((int)numericUpDown1.Value);
            if (_info.Chapters.Count <= 1) return;
            TimeSpan ini = _info.Chapters[0].Time;
            UpdataInfo(ini);
        }
        private void Form1_Move(object sender, EventArgs e)
        {
            if (_previewForm != null)
            {
                _previewForm.Location = new Point(Location.X - 230, Location.Y);
            }
        }

        FormPreview _previewForm;
        private void btnPreview_Click(object sender, EventArgs e)
        {
            if (!IsPathValid) { return; }
            if (_previewForm == null)
            {
                _previewForm = new FormPreview(_info.GetText(cbAutoGenName.Checked), Location);
            }
            _previewForm.UpdateText(_info.GetText(cbAutoGenName.Checked));
            _previewForm.Show();
            _previewForm.Focus();
            _previewForm.Select();
        }

        private void cbAutoGenName_CheckedChanged(object sender, EventArgs e)
        {
            int index = 1;
            if (cbAutoGenName.Checked)
            {
                foreach (var item in dataGridView1.Rows)
                {
                    ((DataGridViewRow) item).Cells[2].Value = $"Chapter {index++:D2}";
                }
                return;
            }
            UpdataGridView();
        }

        private void dataGridView1_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            CTLogger.Log($"+ {e.RowCount} 行被删除");
        }
    }
}