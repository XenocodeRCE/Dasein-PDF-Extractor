using IronOcr;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {

        /*
         Propriétés publiques et statiques 
         */

        /// <summary>
        /// Numéro du lot actuel de copie
        /// </summary>
        public static string lot;

        /// <summary>
        /// Numéro de copie actuelle dans 
        /// le lot actuel de copie
        /// </summary>
        public static int copieActuelle = 1;

        /// <summary>
        /// Page actuelle dans la copie
        /// actuelle dans le lot de copie
        /// actuel... j'ai oublié actuel ? 
        /// </summary>
        public static int pageActuelle = 0;

        /// <summary>
        /// Le PDF avec le lot entier des copies
        /// </summary>
        public static PdfDocument inputDocument;

        /// <summary>
        /// Un chemin vers le dossier où le logiciel
        /// exporte les pages en images pour les travailler
        /// </summary>
        public static string cheminVersExports;

        /// <summary>
        /// Nombre d'image exportée via l'OCR
        /// (le truc juste en dessous là)
        /// </summary>
        public static int imageExportee;


        /// <summary>
        /// L'instance de la librairie qui permet
        /// d'extraire le texte d'une image
        /// </summary>
        public static IronTesseract Ocr;

        /// <summary>
        /// Une mémoire tampon où sont stockés
        /// les chemins vers les images des pages
        /// (utile pour les rassembler dans 1 pdf)
        /// </summary>
        public static List<string> currentCopie;
        public static List<string> currentCopieCropped;

        /// <summary>
        /// Une référence à l'instance actuelle de la Form
        /// </summary>
        private static Form1 frmReference;







        /// <summary>
        /// Indique quand on a commencé
        /// </summary>
        public static DateTime begin;

        /// <summary>
        /// Indique quand la première page a été 
        /// traitée correctement
        /// </summary>
        public static DateTime end;


        public static int totalPages;



        /*
         Code pour la Form
         */

        /// <summary>
        /// Ecrire dans le richtextbox en dehors du Thread UI
        /// </summary>
        /// <param name="what"></param>
        private static void WriteToMyRichTextBox(string what) {
            frmReference.richTextBox1.Invoke((MethodInvoker)delegate {
                // Running on the UI thread
                frmReference.richTextBox1.AppendText(what);
                frmReference.richTextBox1.AppendText(Environment.NewLine);
            });
        }

        private static void UpdateStatuLabel(string what) {

            frmReference.statusStrip1.Invoke(new Action(() => frmReference.toolStripStatusLabel1.Text = what));
            frmReference.statusStrip1.Invoke(new Action(() => frmReference.statusStrip1.Refresh()));

        }


        //toolStripStatusLabel1
        /// <summary>
        /// La méthode d'initialisation de la form1,
        /// la fenetre que vous voyez quand vous
        /// lancez le logiciel
        /// </summary>
        public Form1() {
            InitializeComponent();
            frmReference = this;
            currentCopie = new List<string>();
            currentCopieCropped = new List<string>();

            Ocr = new IronTesseract();
            Ocr.Configuration.BlackListCharacters = "~`$#^*_}{][|\\@";
            Ocr.Configuration.PageSegmentationMode = TesseractPageSegmentationMode.Auto;
            Ocr.Configuration.TesseractVersion = TesseractVersion.Tesseract5;
            Ocr.Configuration.EngineMode = TesseractEngineMode.LstmOnly;
        }

        /// <summary>
        /// Le code d'action du bouton "Charger PDF"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e) {
            OpenFileDialog ofd = new OpenFileDialog {
                Filter = "Pdf files (*.pdf)|*.pdf",
                Title = "Selectionnez votre lot de copie (gros fichier PDF)"
            };
            if (ofd.ShowDialog() == DialogResult.OK) {
                string filePath = ofd.FileName;
                string safeFilePath = ofd.SafeFileName;
                txt_chemin.Text = filePath;
            }
        }


        /// <summary>
        /// Le code d'action du bouton "Analyser et découper"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e) {

            if(txt_lot.Text == "") {
                MessageBox.Show("Veuillez entrer le numéro de lot ! s.v.p");
            }else if (txt_chemin.Text == "") {
                MessageBox.Show("Veuillez charger votre lot au format PDF ! s.v.p");
            } else {
                begin = DateTime.Now;

                lot = txt_lot.Text;
                if (!Directory.Exists(Directory.GetCurrentDirectory() + $"\\{lot}\\")) {
                    Directory.CreateDirectory(Directory.GetCurrentDirectory() + $"\\{lot}\\");
                }
                cheminVersExports = Directory.GetCurrentDirectory() + $"\\{lot}\\";

                inputDocument = PdfReader.Open(txt_chemin.Text, PdfDocumentOpenMode.Modify);
                backgroundWorker1.RunWorkerAsync();
            }

        }



        /*
         Code qui fait un truc utile de fait
         */

        static void ExportImage(PdfDictionary image) {
            string filter = image.Elements.GetName("/Filter");
            switch (filter) {
                case "/DCTDecode":
                    ExportJpegImage(image);
                    break;
            }
        }


        static void ExportJpegImage(PdfDictionary image) {
            // Fortunately JPEG has native support in PDF and exporting an image is just writing the stream to a file.
            byte[] stream = image.Stream.Value;
            string path = cheminVersExports + $"{lot}_{imageExportee}.jpeg";
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(stream);
            bw.Close();

            WriteToMyRichTextBox($"Page {pageActuelle} : découpage image");

            Image img = Image.FromFile(path);
            var result = CropImage(img, 0, 0, 2480, 613);
            var newpath = cheminVersExports + $"{lot}_{imageExportee}_cropped.png";
            result.Save(newpath, ImageFormat.Png);
            currentCopieCropped.Add(newpath);

            WriteToMyRichTextBox($"Page {pageActuelle} : récupération texte...");

           

            using (var Input = new OcrInput(newpath)) {
                Input.Deskew(); // removes rotation and perspective
                var Result = Ocr.Read(Input);

                if (!Result.Text.Contains("Bandeau anonymat")) {
                    WriteToMyRichTextBox("La page fait partie de la même copie, on continue...");
                    currentCopie.Add(path);
                } else {
                    var resulta = Result.Text.Substring(Result.Text.LastIndexOf(':') + 1).Trim();
                    var copie = int.Parse(resulta.Replace($"{lot}-", string.Empty));

                    if (copie.ToString("000") == copieActuelle.ToString("000")) {
                        WriteToMyRichTextBox("La page fait partie de la même copie, on continue...");
                        currentCopie.Add(path);
                    } else {
                        WriteToMyRichTextBox("Nouvelle copie !");
                        WriteToMyRichTextBox("Exportation des pages précédentes");

                        PdfDocument doc = new PdfDocument();

                        foreach (var item in currentCopie) {
                            try {
                                string source = item;
                                PdfPage oPage = new PdfPage();

                                doc.Pages.Add(oPage);
                                XGraphics xgr = XGraphics.FromPdfPage(oPage);
                                XImage imga = XImage.FromFile(source);

                                xgr.DrawImage(imga, 0, 0, xgr.PageSize.Width, xgr.PageSize.Height);
                            } catch (Exception ex) {
                                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }


                        string destinaton = Directory.GetCurrentDirectory() + $"\\{lot}_{copieActuelle.ToString("000")}.pdf";
                        doc.Save(destinaton);
                        doc.Close();

                        foreach (var item in currentCopie) {
                            try {
                                File.Delete(item);
                            } catch (Exception ex) {
                            }
                        }

                        foreach (var item in currentCopieCropped) {
                            try {
                                File.Delete(item);
                            } catch (Exception ex) {
                            }
                        }
                        currentCopie = new List<string> {
                            path
                        };
                        copieActuelle++;

                        end = DateTime.Now;
                        var temp_ecoule = (decimal)end.Subtract(begin).TotalSeconds;
                        decimal totalTime = (temp_ecoule * totalPages) / pageActuelle;
                        var remainingTime = totalTime - temp_ecoule;
                        TimeSpan t = TimeSpan.FromSeconds(Decimal.ToDouble(remainingTime));

                        UpdateStatuLabel($"Il vous reste environ {t.Hours} heures {t.Minutes} minutes");
                    }
                }
            }
            imageExportee++;
        }

        public static Bitmap CropImage(Image source, int x, int y, int width, int height) {
            Rectangle crop = new Rectangle(x, y, width, height);

            var bmp = new Bitmap(crop.Width, crop.Height);
            using (var gr = Graphics.FromImage(bmp)) {
                gr.DrawImage(source, new Rectangle(0, 0, bmp.Width, bmp.Height), crop, GraphicsUnit.Pixel);
            }
            return bmp;
        }


        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e) {
            // Iterate pages

            totalPages = inputDocument.PageCount;
            UpdateStatuLabel($"Traitement de la page n°1...");

            foreach (PdfPage page in inputDocument.Pages) {
                pageActuelle++;
                WriteToMyRichTextBox($"Page {pageActuelle} : lecture");
                // Get resources dictionary
                PdfDictionary resources = page.Elements.GetDictionary("/Resources");
                if (resources != null) {
                    // Get external objects dictionary
                    PdfDictionary xObjects = resources.Elements.GetDictionary("/XObject");
                    if (xObjects != null) {
                        ICollection<PdfItem> items = xObjects.Elements.Values;
                        // Iterate references to external objects
                        foreach (PdfItem item in items) {
                            PdfReference reference = item as PdfReference;
                            if (reference != null) {
                                PdfDictionary xObject = reference.Value as PdfDictionary;
                                // Is external object an image?
                                if (xObject != null && xObject.Elements.GetString("/Subtype") == "/Image") {
                                    WriteToMyRichTextBox($"Page {pageActuelle} : extraction image");

                                    ExportImage(xObject);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            MessageBox.Show("Tâche accomplie !");
            UpdateStatuLabel($"Tâche accomplie ! Veuillez trouver vos PDF dans le dossier.");

        }

        private void richTextBox1_TextChanged(object sender, EventArgs e) {
            // set the current caret position to the end
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            // scroll it automatically
            richTextBox1.ScrollToCaret();
        }
    }
}
