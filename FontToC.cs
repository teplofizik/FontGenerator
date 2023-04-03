using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace FontConv
{
    class FontToC
    {
        string _TempFolder = "";
        string _CharArray = "";
        Font _Font;
        List<byte> _Data;   // data array
        List<int> _Index;   // data index

        int _Left;
        int _Right;

        int _MaxHeight;     // max height of symbol

        bool _Mono = false;
        bool _2Bit = false;

        class CharBlock { public int Index; public int Length; public CharBlock(int Offset, int Len) { Index = Offset; Length = Len; } }

        private List<CharBlock> Blocks = new List<CharBlock>();

        public FontToC()
        {
            _Data = new List<byte>();
            _Index = new List<int>();
            _Font = new Font("Times New Roman", 10);

            Blocks.Clear();

            _Left = 0;
            _Right = 0;

            _MaxHeight = 0;
        }

        public bool TwoBitMode
        {
            get { return _2Bit; }
            set { _2Bit = value; }
        }

        public bool Monospaced
        {
            get
            {
                return _Mono;
            }

            set
            {
                _Mono = value;
            }
        }

        public Font SelectedFont
        {
            get
            {
                return _Font;
            }

            set
            {
                _Font = value;
            }
        }

        public string TempFolder
        {
            get
            {
                return _TempFolder;
            }

            set
            {
                _TempFolder = value;
            }
        }

        public string CharArray
        {
            get
            {
                return _CharArray;
            }
        }

        public int Left
        {
            get
            {
                return _Left;
            }

            set
            {
                _Left = value;
            }
        }

        public int Right
        {
            get
            {
                return _Right;
            }

            set
            {
                _Right = value;
            }
        }

        private string W1251ToUnicode(byte src)
        {
            Encoding srcEncodingFormat = Encoding.GetEncoding("windows-1251");
            Encoding dstEncodingFormat = Encoding.Unicode;
            byte[] originalByteString = { src };
            byte[] convertedByteString = Encoding.Convert(srcEncodingFormat,
            dstEncodingFormat, originalByteString);
            return dstEncodingFormat.GetString(convertedByteString);
        }

        public void ConvertAll()
        {
            _Data.Clear();
            _Index.Clear();

            int num_width = 0;
            int max_width = 0;

            for (int i = '0'; i <= '9'; i++) // numbers
                num_width = Math.Max(Convert.ToInt32(GetSymbolSize(Convert.ToByte(i), true).Width), num_width);
            
            for (int i = ' '; i <= 255; i++) // all
                max_width = Math.Max(Convert.ToInt32(GetSymbolSize(Convert.ToByte(i), true).Width), max_width);

            float mintop = float.MaxValue, maxbot = 0;

            for (int i = 0; i < 256; i++) // ANSI table
            {
                RectangleF rect = GetSymbolRect(Convert.ToByte(i), true);
                mintop = Math.Min(rect.Top, mintop);
                maxbot = Math.Max(rect.Bottom, maxbot);    
            }

            for (int i = 0; i < 256; i++) // ANSI table
            {
                int width = 0;
                if (_Mono)
                {
                    width = max_width;
                }
                else
                { 
                    if (i == 32)
                        width = Convert.ToInt32(GetSymbolSize(32, false).Width);
                
                    if ((i >= '0') && (i <= '9'))
                        width = num_width;
                }
                ConvertSymbol(Convert.ToByte(i), width);
            }

            TruncateByHeight();
        }

        public void ConvertString(string Text)
        {
            _Data.Clear();
            _Index.Clear();

            int num_width = 0;
            int max_width = 0;

            for (int i = '0'; i <= '9'; i++) // numbers
                num_width = Math.Max(Convert.ToInt32(GetSymbolSize(Convert.ToByte(i), true).Width), num_width);
        
            for (int i = 1; i < 256; i++)
            {
                if (Text.IndexOf(W1251ToUnicode(Convert.ToByte(i))) >= 0)
                    max_width = Math.Max(Convert.ToInt32(GetSymbolSize(Convert.ToByte(i), true).Width), max_width);
            }

            // First 
            if (Text[0] != ' ')
                ConvertSymbol(Convert.ToByte(32), Convert.ToInt32(GetSymbolSize(32, false).Width)); // first symbol - space

            for (int i = 1; i < 256; i++)
            {
                if (Text.IndexOf(W1251ToUnicode(Convert.ToByte(i))) >= 0)
                {
                    int width = 0;
                    if (_Mono)
                        width = max_width;
                    else
                    {
                        if (i == 32)
                            width = Convert.ToInt32(GetSymbolSize(32, false).Width);

                        if ((i >= '0') && (i <= '9'))
                            width = num_width;
                    }
                    ConvertSymbol(Convert.ToByte(i), width);
                }
                else
                {
                    // null symbol
                    _Index.Add(0);
                }
            }

            TruncateByHeight();
        }

        void TruncateByHeight()
        {
            int MinAft = 15;
            int MinBef = 15;

            // Calc
            for (int i = 0; i < _Index.Count; i++)
            {
                int Index = _Index[i];

                int W = _Data[Index + 0];
                int H = _Data[Index + 1];
                int BA = _Data[Index + 2];

                int Aft = (BA >> 4) & 0x0F;
                int Bef = BA & 0x0F;

                MinAft = Math.Min(Aft, MinAft);
                MinBef = Math.Min(Bef, MinBef);

                Debug.WriteLine(String.Format("F[{0:d}]={1:d}; {2:d},{3:d}", i, Index, Bef, Aft));
            }

            Debug.WriteLine(String.Format("Min={0:d},{1:d}", MinBef, MinAft));

            // Fix
            var Fixed = new List<int>();

            for (int i = 0; i < _Index.Count; i++)
            {
                int Index = _Index[i];

                if (!Fixed.Contains(Index))
                {
                    int BA = _Data[Index + 2];

                    int Aft = (BA >> 4) & 0x0F;
                    int Bef = BA & 0x0F;

                    Aft -= MinAft;
                    Bef -= MinBef;

                    _Data[Index + 2] = Convert.ToByte((Aft << 4) | Bef);

                    Fixed.Add(Index);
                }
            }

            _MaxHeight -= MinBef + MinAft;
        }

        SizeF GetSymbolSize(byte symb, bool truncate)
        {
            RectangleF rect = GetSymbolRect(symb, truncate);

            return new SizeF(rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        RectangleF GetSymbolRect(byte symb, bool truncate)
        {
            Bitmap bmp = new Bitmap(10, 10);

            string symbol = W1251ToUnicode(symb);
            Graphics surface = Graphics.FromImage(bmp);
            SizeF size = surface.MeasureString(symbol, _Font);
            
            surface.Dispose();
            bmp.Dispose();

            if (!truncate)
            {
                return new RectangleF(0, 0, size.Width, size.Height);
            }

            int h = Convert.ToInt32(size.Height);
            int w = Convert.ToInt32(size.Width);

            bmp = new Bitmap(w, h);
            surface = Graphics.FromImage(bmp);
            surface.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            surface.DrawString(symbol, _Font, new SolidBrush(Color.Black), new PointF(0, 0));

            int counter = 0;
            int lines_bef = 0;
            int lines_aft = 0;
            int lines_left = 0;
            int lines_right = 0;

            // Cut empty lines
            while (counter < h)
            {
                int calc_color = 0;
                for (int x = 0; x < w; x++)
                {
                    Color color = bmp.GetPixel(x, counter);
                    calc_color += color.A;
                }
                if (calc_color != 0) // not empty line
                {
                    lines_bef = counter;
                    break;
                }
                counter++;
            }
            if (counter == h) lines_bef = h;

            counter = h - 1;
            while (counter >= 0)
            {
                int calc_color = 0;
                for (int x = 0; x < w; x++)
                {
                    Color color = bmp.GetPixel(x, counter);
                    calc_color += color.A;
                }
                if (calc_color != 0) // not empty line
                {
                    lines_aft = h - counter - 1;
                    break;
                }
                counter--;
            }
            if (counter == -1) lines_aft = -1;

            counter = 0;
            while (counter < w)
            {
                int calc_color = 0;
                for (int y = 0; y < h; y++)
                {
                    Color color = bmp.GetPixel(counter, y);
                    calc_color += color.A;
                }
                if (calc_color != 0) // not empty line
                {
                    lines_left = counter;
                    counter = 0;
                    break;
                }
                counter++;
            }
            if (counter > 0) lines_left = counter;

            counter = 0;
            while (counter < w)
            {
                int calc_color = 0;
                for (int y = 0; y < h; y++)
                {
                    Color color = bmp.GetPixel(w - counter - 1, y);
                    calc_color += color.A;
                }
                if (calc_color != 0) // not empty line
                {
                    lines_right = counter;
                    counter = 0;
                    break;
                }
                counter++;
            }
            if (counter > 0) lines_right = counter;

            if ((lines_left == w) && (lines_right == w)) { lines_right = w / 2; lines_left = w / 2; };
            if ((lines_left + lines_right) > w) { lines_right = w - lines_left; };
            int _w = w - lines_left - lines_right + _Left + _Right;
            
            bmp.Dispose();
            surface.Dispose();

            return new RectangleF(lines_left - _Left, lines_bef, _w, h - lines_aft - lines_bef);
        }

        public void ConvertSymbol(byte symb, int width)
        {
            SizeF size = GetSymbolSize(symb, false);

            Bitmap bmp = new Bitmap(Convert.ToInt32(size.Width), Convert.ToInt32(size.Height));

            int w = bmp.Width;
            int h = bmp.Height;

            int cl = _Left;
            int cr = _Right;
            // TODO: change algorithm
            if (bmp.Width - (_Left + _Right) > 10)
            {
                w -= (cl + cr);
            }
            else
            {
                cl = 0;
                cr = 0;
            }

            if (symb > 32)
            {
                _MaxHeight = Math.Max(_MaxHeight, h);
            }

            Graphics surface = Graphics.FromImage(bmp);

            string symbol = W1251ToUnicode(symb);
            surface.TextRenderingHint = (_2Bit) ? System.Drawing.Text.TextRenderingHint.AntiAlias : System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            surface.DrawString(symbol, _Font, new SolidBrush(Color.Black), new PointF(0, 0));

            if (symb == 48) // '0'
            {
                //_MaxHeight = _MaxHeight;
            }

            int counter = 0;
            int lines_bef = 0;
            int lines_aft = 0;
            int lines_left = 0;
            int lines_right = 0;

            // Cut empty lines
            while (counter < h)
            {
                int calc_color = 0;
                for (int x = 0; x < w; x++)
                {
                    Color color = bmp.GetPixel(x, counter);
                    calc_color += color.A;
                }
                if (calc_color != 0) // not empty line
                {
                    lines_bef = counter;
                    break;
                }
                counter++;
            }
            if (counter == h) lines_bef = h;

            counter = h - 1;
            while (counter >= 0)
            {
                int calc_color = 0;
                for (int x = 0; x < w; x++)
                {
                    Color color = bmp.GetPixel(x, counter);
                    calc_color += color.A;
                }
                if (calc_color != 0) // not empty line
                {
                    lines_aft = h - counter - 1;
                    break;
                }
                counter--;
            }
            if (counter == -1) lines_aft = h - 1;

            counter = 0;
            while (counter < w)
            {
                int calc_color = 0;
                for (int y = 0; y < h; y++)
                {
                    Color color = bmp.GetPixel(counter, y);
                    calc_color += color.A;
                }
                if (calc_color != 0) // not empty line
                {
                    lines_left = counter;
                    counter = 0;
                    break;
                }
                counter++;
            }
            if (counter > 0) lines_left = counter;

            counter = 0;
            while (counter < w)
            {
                int calc_color = 0;
                for (int y = 0; y < h; y++)
                {
                    Color color = bmp.GetPixel(w - counter - 1, y);
                    calc_color += color.A;
                }
                if (calc_color != 0) // not empty line
                {
                    lines_right = counter;
                    counter = 0;
                    break;
                }
                counter++;
            }
            if (counter > 0) lines_right = counter;
            
            if (lines_bef > 15) lines_bef = 15;
            if (lines_aft > 15) lines_aft = 15;

            if ((lines_aft + lines_bef) > h) { lines_aft = h - lines_bef; };
            if ((lines_left == w) && (lines_right == w)) { lines_right = w / 2; lines_left = w / 2; };

            if ((lines_left + lines_right) > w) { lines_right = w - lines_left; };

            int _w = w - lines_left - lines_right + _Left + _Right;
            if (width > 0)
            {
                int __w = _w;

                _w = width;
                if (width >= w)
                {
                    lines_left = _Left;
                    _w = w;
                }
                else //if (width >= _w) (width < _w)
                {
                    lines_left -= (width - __w) / 2;
                }

                if (lines_left < _Left) lines_left = _Left;
            }

            int LastOffset = _Data.Count;
            _Index.Add(_Data.Count);

            _Data.Add(Convert.ToByte(_w));   // w
            _Data.Add(Convert.ToByte(h - lines_aft - lines_bef));   // h
            _Data.Add(Convert.ToByte((lines_bef & 0x0F) + ((lines_aft << 4) & 0xF0))); // How many lines skip before textout

            byte temp = 0;
            byte cntr = 0;
            counter = 0;

            while (counter < _w * (h - lines_aft - lines_bef))
            {
                int x = (counter % _w) + lines_left - _Left;
                int y = (counter / _w) + lines_bef;

                Color color = (x >= bmp.Width) ? Color.FromArgb(0) : bmp.GetPixel(x, y);

                if (_2Bit)
                {
                    int C = color.A >> 6; // Only 2 bit

                    temp |= Convert.ToByte(C << cntr);
                    if (cntr == 6) // byte is full
                    {
                        _Data.Add(temp);
                        temp = 0;
                        cntr = 0;
                    }
                    else
                        cntr += 2;
                }
                else
                {
                    if (color.A > 50)
                    {
                        temp |= Convert.ToByte(1 << cntr);
                    }

                    if (cntr == 7) // byte is full
                    {
                        _Data.Add(temp);
                        temp = 0;
                        cntr = 0;
                    }
                    else
                        cntr++;
                }

                counter++;
            }

            if (cntr > 0)
            {
                _Data.Add(temp);
            }
            Blocks.Add(new CharBlock(LastOffset, _Data.Count - LastOffset));

            if (_TempFolder != "")
            {
                if (!Directory.Exists(_TempFolder))
                {
                    Directory.CreateDirectory(_TempFolder);
                }
                int ord = symbol[0];
                if (_TempFolder != "")
                {
                    bmp.Save(_TempFolder + Convert.ToString(Convert.ToInt16(symb)) + ".png", System.Drawing.Imaging.ImageFormat.Png);
                }
                bmp.Dispose();
            }
        }

        public void _Test()
        {
            if (!((_Data.Count > 0) && (_Index.Count > 0)))    
            {
                // no data
                return;
            }

            // Ok, data nya
            int index = 0; // index of symbol;
            int offset = 0; // data offset

            while (index < _Index.Count)
            {
                if (offset >= _Data.Count) break;
 //               int offsetx = _Index[int];
                int w = _Data[offset];
                int h = _Data[offset + 1];
                int bef = _Data[offset + 2] & 0x0F;
                int aft = (_Data[offset + 2] & 0xF0) >> 4; 

                offset += 3;
                Bitmap bmp = new Bitmap(w, h + bef + aft);

                int bit_counter = 0;
                int temp = 0;
                int counter = 0;
                while (counter < w * h)
                {
                    int x = counter % w;
                    int y = counter / w + bef;
                    if (bit_counter == 0)
                    {
                        temp = _Data[offset];
                        offset += 1;
                    }

                    if(_2Bit)
                    {
                        switch (temp & 3)
                        {
                            case 0: break;
                            case 1: bmp.SetPixel(x, y, Color.FromArgb(170, 170, 170)); break;
                            case 2: bmp.SetPixel(x, y, Color.FromArgb(85, 85, 85)); break;
                            case 3: bmp.SetPixel(x, y, Color.Black); break;
                        }

                        temp >>= 2;
                        bit_counter += 2;
                    }
                    else
                    {
                        if ((temp & 1) == 1)
                        {
                            bmp.SetPixel(x, y, Color.Black);
                        }

                        temp >>= 1;
                        bit_counter++;
                    }
                    counter++;
                    if (bit_counter == 8) bit_counter = 0;
                }

                if (_TempFolder != "")
                {
                    bmp.Save(_TempFolder + Convert.ToString(index) + "_unpack.png", System.Drawing.Imaging.ImageFormat.Png);
                }
                bmp.Dispose();
                index++;
            }
        }

        private string GetBytesLine(int Offset, int Length)
        {
            int counter = Length;
            string _text = "";
            for (int i = 0; i < Length; i++)
            {
                if (i < 2)
                    _text += string.Format("{0}, ", _Data[Offset + i]);
                else
                    _text += string.Format("0x{0:x2}, ", _Data[Offset + i]);
            }
            return _text;
        }

        public void SaveFile(string filename, string fontname)
        {
            fontname = fontname.ToLower();

            string _text = "";

            _text += "// Font data for graphical project \r\n";
            _text += "// use: SetFont(<index>, &" + fontname + "); \r\n\r\n";
            _text += "// use: SetFont(<index>, " +
                     string.Format("(uint8_t *){0:s}_index, (uint8_t *){0:s}_data, {1:s}_CHAR_COUNT, {1:s}_MAX_HEIGHT", 
                                   fontname,
                                   fontname.ToUpper()) + "); \r\n\r\n";

            _text += "#include <stdint.h>\r\n";

            _text += "// Count of characters in a font \r\n";
            _text += "#define " + fontname.ToUpper() + "_CHAR_COUNT\t" +
                     Convert.ToString(_Index.Count) + "\r\n";
            _text += "// Max height of characters in a font \r\n";
            _text += "#define " + fontname.ToUpper() + "_MAX_HEIGHT\t" +
                     Convert.ToString(_MaxHeight) + "\r\n\r\n";

            _text += "// Character data offset from start of array (2 byte per character): \r\n";
            string temp = string.Format("const uint8_t {0:S}_index [{1:D}] = ", fontname, _Index.Count * 2) + "{";
            _text += temp;

            string spaces = "";
            for (int i = 0; i < temp.Length; i++)
            {
                spaces += " ";
            }

            for (int i = 0; i < _Index.Count; i++)
            {
                _text += string.Format("0x{0:x2}, 0x{1:x2}, // ", // {2:S}
                                               (_Index[i] & 0xFF00)>>8,
                                               _Index[i] & 0xFF, 
                                               W1251ToUnicode(Convert.ToByte((i<32)? 32 : i))) + "\r\n" + spaces;
            }
            _text += "};\r\n\r\n";

            _text += "// Character data: \r\n";
            temp = string.Format("const uint8_t {0:S}_data [{1:D}] = ", fontname, _Data.Count * 2) + "{" + "\r\n" + "   ";
            _text += temp;

            spaces = "";
            for (int i = 0; i < temp.Length; i++)
            {
                spaces += " ";
            }

            int Test = 0;
            foreach(var B in Blocks)
            {
                Test += B.Length;
                _text += GetBytesLine(B.Index, B.Length) + "\r\n" + "   ";
            }

            if (Test != _Data.Count)
                Debug.WriteLine("Invalid write!");

            /*
            int bytes_on_line = 16;
            int counter = _Data.Count;
            while (counter > 0)
            {
                temp = "";
                int t_cnt = counter;
                for (int i = 0; i < Math.Min(bytes_on_line, t_cnt); i++)
                {
                    _text += string.Format("0x{0:x2}, ", _Data[_Data.Count - counter]);

                    counter--;
                }
                _text += "\r\n" + spaces;
            }*/
            _text += "};\r\n\r\n";

            _text += "const sFontRec " + fontname + " = {" +
                string.Format(" {0:d}, (uint8_t*){1:s}_index, (uint8_t*){1:s}_data, {2:d}, {3:d} ", _2Bit ? 2 : 1, fontname, _Index.Count, _MaxHeight) + "};";

            TextWriter tw = new StreamWriter(filename);
            tw.Write(_text);
            tw.Close();
        }

        public string ConvertString(string text, string varname)
        {
            // string to convert
            string _text = "";

            string temp = string.Format("const uint8_t {0:S} [{1:D}] = ", varname, text.Length+1) + "{";
            _text += temp;

            string spaces = "";
            for (int i = 0; i < temp.Length; i++)
            {
                spaces += " ";
            }

            // String length
            _text += string.Format("0x{0:x2}, ", text.Length);

            // String data
            int bytes_on_line = 16;
            int counter = text.Length - 1;
            while (counter > 0)
            {
                temp = "";
                int t_cnt = counter;
                for (int i = 0; i < Math.Min(bytes_on_line, t_cnt); i++)
                {
                    int index = GetIndexByChar(text[i]);
                    _text += string.Format("0x{0:x2}, ", index);

                    counter--;
                }
                _text += "\r\n" + spaces;
            }
            _text += "};\r\n";

            return _text;
        }

        int GetIndexByChar(char Char)
        {
            for(int i = 0; i < _CharArray.Length; i++)
            {
                if (Char == _CharArray[i]) return i;
            }

            return 0;
        }
    }
}
