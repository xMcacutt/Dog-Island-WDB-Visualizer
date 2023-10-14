using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dog_Island_WDB_Visualizer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "WDB Files|*.wdb";
            var result = dlg.ShowDialog();
            if (result != DialogResult.OK) return;
            string[] text = ParseFile(dlg.FileName);
            ColorizeText(text);
        }

        private string[] ParseFile(string filename)
        {
            List<string> lines = new List<string>();
            var fs = File.OpenRead(filename);
            int dialogCount = BitConverter.ToInt32(ReadBytes(fs, 4), 0);
            int descriptorsOffset = BitConverter.ToInt32(ReadBytes(fs, 4), 0);
            int dialogNamesOffset = BitConverter.ToInt32(ReadBytes(fs, 4), 0);
            int dialogsOffset = BitConverter.ToInt32(ReadBytes(fs, 4), 0);

            for(int i = 0; i < dialogCount; i++)
            {
                fs.Seek(descriptorsOffset + 0x18 * i, SeekOrigin.Begin);
                int dialogNameOffset = BitConverter.ToInt32(ReadBytes(fs, 4), 0);
                bool selectableLine = BitConverter.ToInt32(ReadBytes(fs, 4), 0) == 0xFFFF;
                int dialogTextOffset = BitConverter.ToInt32(ReadBytes(fs, 4), 0);
                fs.Seek(dialogNameOffset, SeekOrigin.Begin);
                string dialogName = ReadString(fs);
                Console.WriteLine(dialogName);
                if (dialogTextOffset == -1)
                {
                    Console.WriteLine(dialogName);
                    continue;
                }
                fs.Seek(dialogTextOffset, SeekOrigin.Begin);
                string dialogText = ReadString(fs);            
                lines.Add(dialogName + "\n");
                lines.Add(dialogText + "\n");
                lines.Add("\n");
            }
            return lines.ToArray();
        }

        public void ColorizeText(string[] lines)
        {
            richTextBox1.Clear();
            foreach(string line in lines)
            {
                string pattern = @"(#.*?#)";
                string[] segments = Regex.Split(line, pattern);
                Color currentColor = Color.Black;

                foreach (string segment in segments)
                {
                    if (segment.StartsWith("#") && segment.EndsWith("#"))
                    {
                        switch (segment)
                        {
                            case "#n#":
                                richTextBox1.AppendText("\n");
                                break;
                            case "#name#":
                                richTextBox1.AppendText("NAME");
                                break;
                            default:
                                // Change the current color based on the marker and remove the color markers
                                string colorName = segment.Substring(1, segment.Length - 2);
                                currentColor = GetColor(colorName);
                                break;
                        }
                        
                    }
                    else
                    {
                        // Apply the current color to the regular text
                        richTextBox1.SelectionColor = currentColor;
                        richTextBox1.AppendText(segment);
                    }
                }
            }
        }

        private Color GetColor(string colorName)
        {
            switch (colorName)
            {
                case "red":
                    return Color.Red;
                case "black":
                    return Color.Black;
                case "blue":
                    return Color.Blue;
                // Add more color cases as needed
                default:
                    return Color.Black; // Default to black for unrecognized colors
            }
        }

        public static byte[] ReadBytes(FileStream f, int len)
        {
            byte[] buffer = new byte[len];
            f.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        public static string ReadString(FileStream fs)
        {
            string s = "";
            byte b = (byte)fs.ReadByte();
            while(b != 0x0)
            {
                s += Encoding.ASCII.GetString(new byte[] { b });
                b = (byte)fs.ReadByte();
            }
            return s;
        }
    }
}
