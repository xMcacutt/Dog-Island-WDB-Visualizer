using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Dog_Island_WDB_Visualizer
{
    public partial class Form1 : Form
    {
        public static string CurrentFilePath;
        public static bool Translate = false;
        private static readonly string key = "b5c8a9bfdd4e4a50bb5bdaeb095b8660";
        private static readonly string endpoint = "https://api.cognitive.microsofttranslator.com";
        private static readonly string location = "uksouth";

        public Form1()
        {
            InitializeComponent();
            richTextBox1.AllowDrop = true;
            richTextBox1.DragEnter += FileDrag;
            richTextBox1.DragDrop += FileDrop;
        }

        private void FileDrag(object sender, DragEventArgs e)
        {
            // Check if the data being dragged is a file
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy; // Change the cursor to indicate that the data can be copied
            }
            else
            {
                e.Effect = DragDropEffects.None; // Data not accepted
            }
        }

        private void FileDrop(object sender,  DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length != 1) return;

            CurrentFilePath = files[0];
            ParseFile();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "WDB Files|*.wdb|WDS Files|*.wds";
            var result = dlg.ShowDialog();
            if (result != DialogResult.OK) return;
            CurrentFilePath = dlg.FileName;
            ParseFile();
        }

        private void ParseFile()
        {
            string[] text = new string[1];
            if (CurrentFilePath.EndsWith(".wds"))
            {
                text = ParseWDSFile(CurrentFilePath);
                ColorizeText(text);
                return;
            }
            text = ParseWDBFile(CurrentFilePath);
            ColorizeText(text);
        }

        private string[] ParseWDSFile(string filename)
        {
            List<string> lines = new List<string>();
            var fs = File.OpenRead(filename);
            fs.Seek(0x4, SeekOrigin.Begin);
            int dialogDataOffset = BitConverter.ToInt32(ReadBytes(fs, 4), 0);
            int dialogCount = BitConverter.ToInt32(ReadBytes(fs, 4), 0);
            for(int i = 0; i < dialogCount; i++)
            {
                fs.Seek(0x10 + 0x8 * i + 0x4, SeekOrigin.Begin);
                int dialogOffset = BitConverter.ToInt32(ReadBytes(fs, 4), 0);
                dialogOffset += dialogDataOffset;
                int endOfDialog = 0;
                if (i == dialogCount - 1)
                {
                    endOfDialog = (int)fs.Length;
                }
                else
                {
                    fs.Seek(0x4, SeekOrigin.Current);
                    endOfDialog = BitConverter.ToInt32(ReadBytes(fs, 4), 0) - 1;
                    endOfDialog += dialogDataOffset;
                }
                fs.Seek(dialogOffset, SeekOrigin.Begin);
                string dialog = Encoding.GetEncoding("Shift_JIS").GetString(ReadBytes(fs, endOfDialog - dialogOffset));
                if(Translate)
                    dialog = TranslateText(dialog);
                lines.Add(dialog);
            }
            return lines.ToArray();
        }

        private string[] ParseWDBFile(string filename)
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
                if (dialogTextOffset == -1 || dialogNameOffset == -1) continue;
                fs.Seek(dialogNameOffset, SeekOrigin.Begin);
                string dialogName = ReadString(fs);
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
            string nullPattern= "\x00+";  // Match one or more consecutive null bytes
            foreach(string line in lines)
            {
                string fixedLine = Regex.Replace(line, nullPattern, "\n");
                string pattern = @"(#.*?#)";
                string[] segments = Regex.Split(fixedLine, pattern);
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
                            case "#c#":
                                richTextBox1.AppendText("\n");
                                break;
                            case "#elder#":
                                richTextBox1.AppendText("SIBLING");
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
                        Console.WriteLine(segment);
                        richTextBox1.AppendText(segment);
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(richTextBox1.Text))
            {
                try
                {
                    SoundPlayer player = new SoundPlayer("./wvoice_00025.wav");
                    player.Play();
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Error playing alert sound: {ex.Message}");

                }
                MessageBox.Show("File does not contain text.");
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
            while(b != 0x0 && fs.Position < fs.Length)
            {
                s += Encoding.GetEncoding("Shift_JIS").GetString(new byte[] { b });
                b = (byte)fs.ReadByte();
            }
            if (Translate)
            {
                s = TranslateText(s);
            }
            return s;
        }

        public static string TranslateText(string s)
        {
            string route = "/translate?api-version=3.0&from=ja&to=en";
            object[] body = new object[] { new { Text = s } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(endpoint + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", key);
                // location required if you're using a multi-service or regional (not global) resource.
                request.Headers.Add("Ocp-Apim-Subscription-Region", location);

                // Send the request and get response.
                HttpResponseMessage response = client.SendAsync(request).Result;
                // Read response as a string.
                if (response.IsSuccessStatusCode)
                {
                    // Deserialize the JSON response
                    var result = JsonConvert.DeserializeObject<List<TranslationResult>>(response.Content.ReadAsStringAsync().Result);

                    // Extract the translated text
                    string translatedText = result[0].translations[0].text;
                    return translatedText;
                }
                else
                {
                    // Handle the error, e.g., log or throw an exception
                    Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                    return null;
                }
            }
        }

        private void translateToggleClicked(object sender, EventArgs e)
        {
            Translate = !Translate;
            translateToggle.Checked = Translate;
        }
    }

    public class TranslationResult
    {
        public List<Translation> translations { get; set; }
    }

    public class Translation
    {
        public string text { get; set; }
        public string to { get; set; }
    }
}
