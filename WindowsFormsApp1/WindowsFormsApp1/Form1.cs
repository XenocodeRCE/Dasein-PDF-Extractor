using System.Drawing;
using System.Drawing.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;

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
        /// Numero de la dernière page analysée 
        /// où on peut clairement voir que c'est la première
        /// page de la copie
        /// </summary>
        public static int lastVraiCopie = 0;

        /// <summary>
        /// Numero de la dernière page analysée 
        /// associée à <see cref="lastVraiCopie"/>
        /// </summary>
        public static int lastVraiPage = 0;

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
                frmReference.nud_page.Value = pageActuelle;
                frmReference.nud_copie.Value = copieActuelle;

                frmReference.pb_total.Value += 1;
            });
        }

        private static void SetProgressBar() {
            frmReference.statusStrip1.Invoke(new Action(() => frmReference.pb_total.Maximum = totalPages * 10));
            frmReference.statusStrip1.Invoke(new Action(() => frmReference.lbl_total.Text = $"Pages tot° : {totalPages}"));

            //lbl_total
        }
        private static void UpdateProgressBar() {
            frmReference.statusStrip1.Invoke(new Action(() => frmReference.pb_total.Value += 1));
            frmReference.statusStrip1.Invoke(new Action(() => frmReference.pb_total.Update()));
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

                var lot = txt_chemin.Text.Substring(txt_chemin.Text.IndexOf("_lot") + 1).Replace(".pdf", string.Empty).Replace("lot", string.Empty);
                if (IsDigitsOnly(lot) && string.IsNullOrWhiteSpace(txt_lot.Text)) {
                    txt_lot.Text = lot;
                    toolStripStatusLabel1.Text = $"Récupération automatique du numéro de lot : {lot}.";
                } else {
                    toolStripStatusLabel1.Text = "Impossible de récupérer automatiquement le numéro de lot.";
                }
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
                    Directory.CreateDirectory(Directory.GetCurrentDirectory() + $"\\{lot}\\temp\\");

                }
                cheminVersExports = Directory.GetCurrentDirectory() + $"\\{lot}\\";

                inputDocument = PdfReader.Open(txt_chemin.Text, PdfDocumentOpenMode.Modify);

                pageActuelle = Convert.ToInt32(nud_page.Value);
                copieActuelle = Convert.ToInt32(nud_copie.Value);
                nud_page.Enabled = false;
                nud_copie.Enabled = false;
                backgroundWorker1.RunWorkerAsync();
            }

        }



        /*
         Code qui fait un truc utile de fait
         */

        bool IsDigitsOnly(string str) {
            foreach (char c in str) {
                if (c < '0' || c > '9')
                    return false;
            }

            return true;
        }

        static void ExportImage(PdfDictionary image) {
            string filter = image.Elements.GetName("/Filter");
            switch (filter) {
                case "/DCTDecode":
                    ExportJpegImageAsync(image);
                    break;
            }
        }


        static async System.Threading.Tasks.Task ExportJpegImageAsync(PdfDictionary image) {
            // Fortunately JPEG has native support in PDF and exporting an image is just writing the stream to a file.
            byte[] stream = image.Stream.Value;
            string path = cheminVersExports + $"\\temp\\{lot}_{imageExportee}.jpeg";
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(stream);
            bw.Close();

            WriteToMyRichTextBox($"Page {pageActuelle} : découpage image");

            Image img = Image.FromFile(path);
            var result = CropImage(img, 0, 0, 2480, 613);
            
            //rescale 8 fois moins gros
            result = new Bitmap(result, new Size(result.Width / 8, result.Height / 8));

            //noir et blanc
            // Variable for image brightness
            double avgBright = 0;
            for (int y = 0; y < result.Height; y++) {
                for (int x = 0; x < result.Width; x++) {
                    // Get the brightness of this pixel
                    avgBright += result.GetPixel(x, y).GetBrightness();
                }
            }

            // Get the average brightness and limit it's min / max
            avgBright = avgBright / (result.Width * result.Height);
            avgBright = avgBright < .3 ? .3 : avgBright;
            avgBright = avgBright > .7 ? .7 : avgBright;

            // Convert image to black and white based on average brightness
            for (int y = 0; y < result.Height; y++) {
                for (int x = 0; x < result.Width; x++) {
                    // Set this pixel to black or white based on threshold
                    if (result.GetPixel(x, y).GetBrightness() > avgBright) result.SetPixel(x, y, Color.White);
                    else result.SetPixel(x, y, Color.Black);
                }
            }

            // Image is now in black and white

            var newpath = cheminVersExports + $"\\temp\\{ lot}_{imageExportee}_cropped.png";
            result.Save(newpath, ImageFormat.Png);


            currentCopieCropped.Add(newpath);

            WriteToMyRichTextBox($"Page {pageActuelle} : récupération texte...");

            LABEL_RETRY:
            try {

                var task = Task.Run(() => UploadCroppedImage(newpath));
                task.Wait();
                var Result = task.Result;

                Rootobject ocrResult = JsonConvert.DeserializeObject<Rootobject>(Result);

                Result = "";
                if (ocrResult != null) {
                    Result = ocrResult.ParsedResults[0].ParsedText;
                }

                if (!Result.Contains("Bandeau anonymat")) {
                    WriteToMyRichTextBox("La page fait partie de la même copie, on continue...");
                    currentCopie.Add(path);
                } else {
                    var resulta = Result.Substring(Result.IndexOf($"{lot}-")).Trim();

                    var megaman = resulta.Replace($"{lot}-", string.Empty).Substring(0, 3);

                    int copie = 0;
                    try {
                        copie = int.Parse(megaman);
                    } catch (Exception ex) {
                        MessageBox.Show(ex.ToString());
                    }
                    

                    if (copie.ToString("000") == copieActuelle.ToString("000")) {
                        WriteToMyRichTextBox("La page fait partie de la même copie, on continue...");
                        currentCopie.Add(path);
                    } else {
                        WriteToMyRichTextBox("Nouvelle copie !");
                        WriteToMyRichTextBox("Exportation des pages précédentes");
                        lastVraiCopie = copieActuelle;
                        lastVraiPage = pageActuelle;
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


                        string destinaton = cheminVersExports + $"\\{lot}_{copieActuelle.ToString("000")}.pdf";
                        doc.Save(destinaton);
                        doc.Close();

                        
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
                imageExportee++;
            } catch (Exception ex) {
                MessageBox.Show($"Erreur avec la page actuelle {pageActuelle} ! Re-essayons..." );
                goto LABEL_RETRY;

            }

        }

        private static async Task<string> UploadCroppedImage(string path) {
            HttpClient httpClient = new HttpClient {
                Timeout = new TimeSpan(1, 1, 1)
            };


            MultipartFormDataContent form = new MultipartFormDataContent {
                        { new StringContent("sharex889823"), "apikey" }, //Added api key in form data
                        { new StringContent("fre"), "language" }
                    };


            form.Add(new StringContent("2"), "ocrengine");
            form.Add(new StringContent("true"), "scale");
            form.Add(new StringContent("true"), "istable");

            if (string.IsNullOrEmpty(path) == false) {
                byte[] imageData = File.ReadAllBytes(path);
                form.Add(new ByteArrayContent(imageData, 0, imageData.Length), "image", "image.jpg");
            }

            HttpResponseMessage response = await httpClient.PostAsync("https://apipro1.ocr.space/parse/image", form);

            string Result = await response.Content.ReadAsStringAsync();
            return Result;
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

            SetProgressBar();
            UpdateStatuLabel($"Traitement de la copie n°1...");

            if(frmReference.nud_copie.Value != 0) {

                var valuePage = Convert.ToInt32(frmReference.nud_page.Value);
                for (int i = valuePage; i < inputDocument.Pages.Count; i++) {
                    var page = inputDocument.Pages[i];

                    UpdateProgressBar();
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

            } else {

                for (int i = 0; i < inputDocument.Pages.Count; i++) {
                    var page = inputDocument.Pages[i];

                    UpdateProgressBar();
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
            
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            MessageBox.Show("Tâche accomplie !");
            UpdateStatuLabel($"Tâche accomplie ! Veuillez trouver vos PDF dans le dossier.");
            Process.Start(cheminVersExports);


        }

        private void richTextBox1_TextChanged(object sender, EventArgs e) {
            // set the current caret position to the end
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            // scroll it automatically
            richTextBox1.ScrollToCaret();
        }

        private void button3_Click(object sender, EventArgs e) {
            File.WriteAllText("Sauvegarde.txt", $"page: {lastVraiPage} | copie: {lastVraiCopie +1} ");
            MessageBox.Show("La dernière copie entière et la dernière page associée ont été enregistrées dans le dossier racine dans le fichier 'Sauvegarde.txt'. La prochaine fois que vous lancez le logiciel, renseignez le numéro de page et de copie inscrit.");
            Environment.Exit(0);
        }
    }

    public class Rootobject
    {
        public Parsedresult[] ParsedResults { get; set; }
        public int OCRExitCode { get; set; }
        public bool IsErroredOnProcessing { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorDetails { get; set; }
    }

    public class Parsedresult
    {
        public object FileParseExitCode { get; set; }
        public string ParsedText { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorDetails { get; set; }
    }
}
