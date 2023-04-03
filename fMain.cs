using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FontConv
{
    public partial class fMain : Form
    {
        public fMain()
        {
            InitializeComponent();

            dlSave.InitialDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private void bFont_Click(object sender, EventArgs e)
        {
            float FontSize = dlFont.Font.Size;
            if (dlFont.ShowDialog() == DialogResult.Cancel) return;

            lSample.Font = dlFont.Font;

            if (FontSize != dlFont.Font.Size) dlSave.FileName = "";
        }

        private void chAll_CheckedChanged(object sender, EventArgs e)
        {
            gChars.Enabled = !chAll.Checked;
        }

        private string GetFontName()
        {
            if (tName.Text.CompareTo("") == 0)
            {
                return String.Format("f{0:d}", Convert.ToInt32(dlFont.Font.Size));
            }
            else
            {
                return tName.Text;
            }

        }

        private void bSave_Click(object sender, EventArgs e)
        {
            dlSave.Filter = "Заголовочные файлы C (*.h)|*.h|Любые файлы (*.*)|*.*";
            dlSave.Title = "Сохранить код";
            if (dlSave.FileName.CompareTo("") == 0)
                dlSave.FileName = GetFontName() + ".h";
            else
                dlSave.FileName = Path.GetFileName(dlSave.FileName);
            if (dlSave.ShowDialog() == DialogResult.Cancel) return;

            FontToC ftc = new FontToC();
            ftc.SelectedFont = dlFont.Font;
            if(ckImages.Checked) ftc.TempFolder = "img//";

            string sym = " ";
            {
                if (chNumbers.Checked) sym += "0123456789" + ".=+-";
                if (chSpecial.Checked) sym += ".,():;\"" + "?!><%\\/°";

                if (chLatinBig.Checked) sym += "@ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                if (chLatinLittle.Checked) sym += "abcdefghijklmnopqrstuvwxyz";

                if (chRusBig.Checked) sym += "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
                if (chRusLittle.Checked) sym += "абвгдеёжзийклмнопрстуфхцчшщъыьэжюя";
            }

            ftc.TwoBitMode = ck2Bit.Checked;
            ftc.Monospaced = ckMono.Checked;
            ftc.Left = 1;
            ftc.Right = 1;
            if (chAll.Checked)
            {
                ftc.ConvertAll();
            }
            else
            {
                ftc.ConvertString(sym);
            }
            ftc._Test();
            ftc.SaveFile(dlSave.FileName, string.Format("f{0:d}", GetFontName()));
            Text = "Font Converter - готово.";
        }

        private void fMain_Load(object sender, EventArgs e)
        {

        }
    }
}
