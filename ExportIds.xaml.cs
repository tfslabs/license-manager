using HGM.Hotbird64.LicenseManager.Extensions;
using HGM.Hotbird64.Vlmcs;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public enum GuidType
    {
        SkuId = 0,
        KmsId,
    }

    public enum ExportFormat
    {
        Vlmcsd = 0,
        PyKms,
        GenericC,
        GenericCs,
        Xml,
    }

    public partial class ExportIds
    {
        public GuidType GuidType = GuidType.SkuId;
        public ExportFormat ExportFormat = ExportFormat.Xml;
        private const string CCommentFormat = "{0}{2}// {1}";
        private Encoding encoding = Encoding.Default;
        private string tabs = "\t";
        private byte[] vlmcsdBytes;

        public ExportIds(MainWindow mainWindow) : base(mainWindow)
        {
            InitializeComponent();
            Loaded += (s, e) => Icon = this.GenerateImage(new Icons.Export(), 16, 16);
            TopElement.LayoutTransform = Scaler;
            Update();
        }

        private void Update(object sender, RoutedEventArgs e)
        {
            Update();
        }

        private void Update()
        {
            TextBoxOutput.Text = string.Empty;

            if (ExportFormat == ExportFormat.Xml)
            {
                ExportXml();
                return;
            }

            if (ExportFormat == ExportFormat.Vlmcsd)
            {
                ExportVlmcsd();
                return;
            }

            switch (GuidType)
            {
                case GuidType.SkuId:
                    ExportSkuIds();
                    break;

                case GuidType.KmsId:
                    ExportKmsIds();
                    break;

                default:
                    throw new ApplicationException("Unknown GUID Type");
            }
        }

        private void AddOutput(string text)
        {
            TextBoxOutput.AppendText(text + Environment.NewLine);
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void ExportXml()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    NewLineHandling = NewLineHandling.Replace,
                    NewLineOnAttributes = CheckBoxMultiLine.IsChecked.Value,
                    NewLineChars = "\r\n",
                    Encoding = encoding,
                    Indent = true,
                    IndentChars = $"{tabs}",
                };

                XmlWriter writer = XmlWriter.Create(stream, settings);
                XmlSerializer serializer = new XmlSerializer(typeof(KmsData));

                serializer.Serialize(writer, KmsLists.KmsData);
                byte[] buffer = new byte[stream.Length];
                stream.Seek(0, SeekOrigin.Begin);
                stream.Read(buffer, 0, (int)stream.Length);
                string text = encoding.GetString(buffer);

                TextBoxOutput.Text = CheckBoxBlankLines.IsChecked.Value ?
                  Regex.Replace(text, CheckBoxMultiLine.IsChecked.Value ? ".*>" : "<.*>", m =>
                    !CheckBoxMultiLine.IsChecked.Value && (m.Value.StartsWith("<WinBuild") || m.Value.StartsWith("<SkuItem") || m.Value.StartsWith("<HostBuild") || m.Value.StartsWith("<KmsItem") || m.Value.StartsWith("<CsvlkItem") || m.Value.StartsWith("<Activate")) ? m.Value : m.Value.Replace(">", ">\r\n")) :
                  text;
            }
        }

        private void ExportVlmcsd()
        {
            VlmcsdHeader vlmcsdHeader = default(VlmcsdHeader);

#if DEBUG
            Debug.Assert(CheckBoxIncludeApp.IsChecked != null, "CheckBoxIncludeApp.IsChecked != null");
            Debug.Assert(CheckBoxIncludeKms.IsChecked != null, "CheckBoxIncludeKms.IsChecked != null");
            Debug.Assert(CheckBoxIncludeSku.IsChecked != null, "CheckBoxIncludeSku.IsChecked != null");
            Debug.Assert(CheckBoxNoDescription.IsChecked != null, "CheckBoxNoDescription.IsChecked != null");
            Debug.Assert(CheckBoxIncludeBetaSku.IsChecked != null, "CheckBoxIncludeBetaSku.IsChecked != null");
#endif

            using (MemoryStream stream = vlmcsdHeader.WriteData
            (
                CheckBoxIncludeApp.IsChecked.Value,
                CheckBoxIncludeKms.IsChecked.Value,
                CheckBoxIncludeSku.IsChecked.Value,
                CheckBoxNoDescription.IsChecked.Value,
                CheckBoxIncludeBetaSku.IsChecked.Value))
            {
                vlmcsdBytes = stream.ToArray();
            }

            TextBoxOutput.Text = "uint8_t DefaultKmsData[] =\n{\n";

            for (int i = 0; i < vlmcsdBytes.Length; i += 16)
            {
                string text = $"{tabs}/* {i:X4} */ ";

                for (int j = 0; j < 16; j++)
                {
                    if (i + j < vlmcsdBytes.Length)
                    {
                        text += $"0x{vlmcsdBytes[i + j]:X2}, ";
                    }
                    else
                    {
                        text += "      ";
                    }
                }

                text += "  // ";

                for (int j = 0; j < 16; j++)
                {
                    if (i + j >= vlmcsdBytes.Length)
                    {
                        continue;
                    }

                    byte character = vlmcsdBytes[i + j];
                    text += (character < 0x20 || (j == 15 && character == 0x5c)) || (character > 0x7e && character < 0xa1) ? '.' : (char)vlmcsdBytes[i + j];
                }

                TextBoxOutput.AppendText(text);
                TextBoxOutput.AppendText(Environment.NewLine);
            }

            TextBoxOutput.AppendText("};");
        }

        private void ExportKmsIds()
        {
            IOrderedEnumerable<KmsItem> kmsIdList = KmsLists.KmsItemList.OrderBy(k => KmsLists.AppItemList.IndexOf(k.App)).ThenBy(k => k.ToString());

            switch (ExportFormat)
            {
                case ExportFormat.GenericCs:
                    AddExampleCSharpClass();
                    AddOutput("public static readonly KmsGuidModel[] KmsIdList =  new KmsGuidModel[]");
                    AddOutput("{");
                    break;

                case ExportFormat.GenericC:
                    AddExampleCStruct();
                    AddOutput("const KmsItem KmsIdList[] =");
                    AddOutput("{");
                    break;
            }

            foreach (KmsItem kmsId in kmsIdList)
            {
                //var vlmcsdEpidName = kmsId.VlmcsdEpidNameSpecified ? kmsId.VlmcsdEpidName.ToString() : kmsId.App.VlmcsdEpidName.ToString();
                //var friendlyNamePadding = new string(' ', maxFriendlyNameLength - kmsId.ToString().Length);
                //var ePidMacroPadding = new string(' ', maxEpidMacroLength - vlmcsdEpidName.Length);
                // ReSharper disable once PossibleInvalidOperationException
                string guidComment = CheckBoxAddComment.IsChecked.Value ? $" /*{kmsId.Guid}*/" : string.Empty;
                string part4 = kmsId.Guid.Part4.Aggregate("", (current, guidByte) => current + $"0x{guidByte:x2}, ");

                switch (ExportFormat)
                {
                    //case ExportFormat.Vlmcsd:
                    //  if (kmsId.VlmcsdNameSpecified && kmsId.VlmcsdName == VlmcsdKmsIds.KMS_ID_VISTA) AddOutput($"#{tabs}ifndef NO_BASIC_PRODUCT_LIST");
                    //  if (kmsId.VlmcsdName >= 0) AddOutput($"{tabs}/* {(int)kmsId.VlmcsdName:000} */ {{ {{ 0x{kmsId.Guid.Part1:x8}, 0x{kmsId.Guid.Part2:x4}, 0x{kmsId.Guid.Part3:x4}, {{ {part4}}} }}{guidComment}, LOGTEXT(\"{kmsId}\"),{friendlyNamePadding} {vlmcsdEpidName},{ePidMacroPadding} {kmsId.DefaultKmsProtocol.Major:#}, {kmsId.NCountPolicy,2} }},");
                    //  if (kmsId.VlmcsdNameSpecified && kmsId == kmsIdList.Last()) AddOutput($"#{tabs}endif // NO_BASIC_PRODUCT_LIST");
                    //  break;

                    case ExportFormat.GenericCs:
                        AddOutput($"{tabs}new KmsGuidModel {{ Guid = new Guid(0x{kmsId.Guid.Part1:x8}, 0x{kmsId.Guid.Part2:x4}, 0x{kmsId.Guid.Part3:x4}, {part4.Substring(0, part4.Length - 2)}){guidComment}, DisplayName = \"{kmsId}\" }}, ");
                        break;

                    case ExportFormat.GenericC:
                        AddOutput($"{tabs}{{ {{ 0x{kmsId.Guid.Part1:x8}, 0x{kmsId.Guid.Part2:x4}, 0x{kmsId.Guid.Part3:x4}, {{ {part4}}} }}{guidComment}, \"{kmsId}\" }},");
                        break;

                    default:
                        throw new NotSupportedException("Invalid Export Format");
                }
            }

            AddFooter();
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void ExportSkuIds()
        {
            switch (ExportFormat)
            {
                case ExportFormat.GenericCs:
                    AddExampleCSharpClass();
                    AddOutput("public static readonly KmsGuidModel[] SkuIdList =  new KmsGuidModel[]");
                    AddOutput("{");
                    break;

                case ExportFormat.GenericC:
                    AddExampleCStruct();
                    AddOutput("const KmsItem SkuIdList[] =");
                    AddOutput("{");
                    break;
            }

            foreach (AppItem appId in KmsLists.AppItemList)
            {
                KmsItem[] kmsIdList = KmsLists.KmsItemList.Where(k => k.App == appId).OrderBy(k => k.DisplayName).ToArray();

                foreach (KmsItem kmsId in kmsIdList)
                {
                    IOrderedEnumerable<SkuItem> skuIdList = KmsLists.SkuItemList.Where(s => s.KmsItem == kmsId).OrderBy(s => s.DisplayName);
                    if (ExportFormat != ExportFormat.PyKms)
                    {
                        AddOutput(string.Format(CCommentFormat, appId != KmsLists.AppItemList.First() || kmsId != kmsIdList.First() ? Environment.NewLine : "", kmsId, tabs));
                    }

                    if (kmsId.IsPreview && ExportFormat == ExportFormat.Vlmcsd)
                    {
                        AddOutput($"#{tabs}ifdef INCLUDE_BETAS");
                    }

                    foreach (SkuItem skuId in skuIdList)
                    {
                        string part4 = skuId.Guid.Part4.Aggregate("", (current, guidByte) => current + $"0x{guidByte:x2}, ");
                        //var vlmcsdEpidName = skuId.KmsItem.VlmcsdEpidNameSpecified ? skuId.KmsItem.VlmcsdEpidName.ToString() : skuId.KmsItem.App.VlmcsdEpidName.ToString();
                        //var vlmcsdName = skuId.KmsItem.App.VlmcsdName.ToString();
                        //var vlmcsdKmsIdName = skuId.KmsItem.VlmcsdName.ToString();

                        //var friendlyNamePadding = new string(' ', maxFriendlyNameLength - skuId.ToString().Length);
                        //var ePidMacroPadding = new string(' ', maxEpidMacroLength - vlmcsdEpidName.Length);
                        //var appMacroPadding = new string(' ', maxAppMacroLength - vlmcsdName.Length);
                        string guidComment = CheckBoxAddComment.IsChecked.Value ? $" /*{skuId.Guid}*/" : string.Empty;

                        switch (ExportFormat)
                        {
                            //case ExportFormat.Vlmcsd:
                            //  AddOutput($"{tabs}{{ {{ 0x{skuId.Guid.Part1:x8}, 0x{skuId.Guid.Part2:x4}, 0x{skuId.Guid.Part3:x4}, {{ {part4}}} }}{guidComment}, LOGTEXT(\"{skuId}\"),{friendlyNamePadding} {vlmcsdEpidName},{ePidMacroPadding} {vlmcsdName},{appMacroPadding} {vlmcsdKmsIdName} }},");
                            //  break;

                            case ExportFormat.PyKms:
                                AddOutput($"{tabs}{tabs}uuid.UUID(\"{skuId.Guid}\") : \"{skuId}\",");
                                break;

                            case ExportFormat.GenericC:
                                AddOutput($"{tabs}{{ {{ 0x{skuId.Guid.Part1:x8}, 0x{skuId.Guid.Part2:x4}, 0x{skuId.Guid.Part3:x4}, {{ {part4}}} }}{guidComment}, \"{skuId}\" }},");
                                break;

                            case ExportFormat.GenericCs:
                                AddOutput($"{tabs}new KmsGuidModel {{ Guid = new Guid(0x{skuId.Guid.Part1:x8}, 0x{skuId.Guid.Part2:x4}, 0x{skuId.Guid.Part3:x4}, {part4.Substring(0, part4.Length - 2)}){guidComment}, DisplayName = \"{skuId}\" }}, ");
                                break;

                            default:
                                throw new NotSupportedException("Invalid Export Format");
                        }
                    }

                    if (kmsId.IsPreview && ExportFormat == ExportFormat.Vlmcsd)
                    {
                        AddOutput($"#{tabs}endif // INCLUDE_BETAS");
                    }
                }
            }

            AddFooter();
        }

        private void AddFooter()
        {
            switch (ExportFormat)
            {
                case ExportFormat.GenericC:
                case ExportFormat.GenericCs:
                    AddOutput("};");
                    break;
            }
        }

        private void AddExampleCSharpClass()
        {
            AddOutput("// Example class definition");
            AddOutput("public class KmsGuidModel");
            AddOutput("{");
            AddOutput($"{tabs}public Guid Guid {{ get; set; }}");
            AddOutput($"{tabs}public string DisplayName {{ get; set; }}");
            AddOutput("}");
            AddOutput("");
        }

        private void AddExampleCStruct()
        {
            AddOutput("// Example typedef");
            AddOutput("typedef struct kmsItem");
            AddOutput("{");
            AddOutput($"{tabs}GUID Guid;");
            AddOutput($"{tabs}const char* Name;");
            AddOutput("} KmsItem;");
            AddOutput("");
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog()
            {
                CheckPathExists = true,
                AddExtension = false,
                OverwritePrompt = true,
                DereferenceLinks = true,
                ValidateNames = true,
                Title = (string)ButtonSaveAsAscii.Content,
            };

            switch (ExportFormat)
            {
                case ExportFormat.Xml:
                    dialog.Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*";
                    break;

                case ExportFormat.Vlmcsd:
                    dialog.Filter = "KMS Data Files (*.kmd)|*.kmd|All Files (*.*)|*";
                    break;

                default:
                    dialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*";
                    break;
            }

            bool? result = dialog.ShowDialog(this);
            if (!result.Value)
            {
                return;
            }

            try
            {
                ButtonSaveAsAscii.IsEnabled = false;

                if (ExportFormat != ExportFormat.Vlmcsd)
                {
                    string textToWrite = TextBoxOutput.Text;
                    await Task.Run(() => File.WriteAllText(dialog.FileName, textToWrite, encoding));
                }
                else
                {
                    await Task.Run(() => File.WriteAllBytes(dialog.FileName, vlmcsdBytes));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, $"Error Writing File \"{dialog.SafeFileName}\"", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ButtonSaveAsAscii.IsEnabled = true;
            }
        }

        private void RadioButton_SkuIds_Click(object sender, RoutedEventArgs e)
        {
            GuidType = GuidType.SkuId;
            Update();
        }

        private void RadioButton_KmsIds_Click(object sender, RoutedEventArgs e)
        {
            GuidType = GuidType.KmsId;
            Update();
        }

        private void RadioButton_Vlmcsd_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxUseTabs.Visibility = RadioButtonKmsIds.Visibility = Visibility.Visible;
            ExportFormat = ExportFormat.Vlmcsd;
            GroupBoxGuidType.IsEnabled = true;
            CheckBoxAddComment.Visibility = CheckBoxMultiLine.Visibility = CheckBoxBlankLines.Visibility = Visibility.Collapsed;
            EncodingPanel.Visibility = Visibility.Collapsed;
            GroupBoxGuidType.IsEnabled = false;
            CheckBoxIncludeBetaSku.Visibility = CheckBoxNoDescription.Visibility = CheckBoxIncludeApp.Visibility = CheckBoxIncludeKms.Visibility = CheckBoxIncludeSku.Visibility = Visibility.Visible;
            Update();
        }

        private void RadioButton_PyKms_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxAddComment.Visibility = RadioButtonKmsIds.Visibility = Visibility.Collapsed;
            RadioButtonSkuIds.IsChecked = true;
            ExportFormat = ExportFormat.PyKms;
            GuidType = GuidType.SkuId;
            GroupBoxGuidType.IsEnabled = true;
            CheckBoxMultiLine.Visibility = CheckBoxBlankLines.Visibility = Visibility.Collapsed;
            CheckBoxUseTabs.Visibility = EncodingPanel.Visibility = Visibility.Visible;
            CheckBoxIncludeBetaSku.Visibility = CheckBoxNoDescription.Visibility = CheckBoxIncludeApp.Visibility = CheckBoxIncludeKms.Visibility = CheckBoxIncludeSku.Visibility = Visibility.Collapsed;
            Update();
        }

        private void RadioButton_GenericC_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxAddComment.Visibility = RadioButtonKmsIds.Visibility = Visibility.Visible;
            ExportFormat = ExportFormat.GenericC;
            GroupBoxGuidType.IsEnabled = true;
            CheckBoxMultiLine.Visibility = CheckBoxBlankLines.Visibility = Visibility.Collapsed;
            CheckBoxUseTabs.Visibility = EncodingPanel.Visibility = Visibility.Visible;
            CheckBoxIncludeBetaSku.Visibility = CheckBoxNoDescription.Visibility = CheckBoxIncludeApp.Visibility = CheckBoxIncludeKms.Visibility = CheckBoxIncludeSku.Visibility = Visibility.Collapsed;
            Update();
        }

        private void RadioButton_GenericCS_Click(object sender, RoutedEventArgs e)
        {
            CheckBoxAddComment.Visibility = RadioButtonKmsIds.Visibility = Visibility.Visible;
            ExportFormat = ExportFormat.GenericCs;
            GroupBoxGuidType.IsEnabled = true;
            CheckBoxMultiLine.Visibility = CheckBoxBlankLines.Visibility = Visibility.Collapsed;
            CheckBoxUseTabs.Visibility = EncodingPanel.Visibility = Visibility.Visible;
            CheckBoxIncludeBetaSku.Visibility = CheckBoxNoDescription.Visibility = CheckBoxIncludeApp.Visibility = CheckBoxIncludeKms.Visibility = CheckBoxIncludeSku.Visibility = Visibility.Collapsed;
            Update();
        }

        private void RadioButton_Xml_Click(object sender, RoutedEventArgs e)
        {
            RadioButtonKmsIds.Visibility = Visibility.Visible;
            CheckBoxAddComment.Visibility = Visibility.Collapsed;
            ExportFormat = ExportFormat.Xml;
            GroupBoxGuidType.IsEnabled = false;
            RadioButtonUtf8.IsChecked = true;
            encoding = Encoding.UTF8;
            CheckBoxMultiLine.Visibility = CheckBoxBlankLines.Visibility = Visibility.Visible;
            CheckBoxUseTabs.Visibility = EncodingPanel.Visibility = Visibility.Visible;
            CheckBoxIncludeBetaSku.Visibility = CheckBoxNoDescription.Visibility = CheckBoxIncludeApp.Visibility = CheckBoxIncludeKms.Visibility = CheckBoxIncludeSku.Visibility = Visibility.Collapsed;
            Update();
        }

        private void RadioButton_Ansi_Click(object sender, RoutedEventArgs e)
        {
            encoding = Encoding.Default;
            if (ExportFormat == ExportFormat.Xml)
            {
                Update();
            }
        }

        private void RadioButton_Utf8_Click(object sender, RoutedEventArgs e)
        {
            encoding = Encoding.UTF8;
            if (ExportFormat == ExportFormat.Xml)
            {
                Update();
            }
        }

        private void RadioButton_Utf16Le_Click(object sender, RoutedEventArgs e)
        {
            encoding = Encoding.Unicode;
            if (ExportFormat == ExportFormat.Xml)
            {
                Update();
            }
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        private void CheckBox_UseTabs_Click(object sender, RoutedEventArgs e)
        {
            tabs = CheckBoxUseTabs.IsChecked.Value ? "\t" : "    ";
            Update();
        }

        private void CheckBoxIncludeKms_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Debug.Assert(CheckBoxIncludeKms.IsChecked != null, "CheckBoxIncludeKms.IsChecked != null");
#endif

            if (!CheckBoxIncludeKms.IsChecked.Value)
            {
                CheckBoxIncludeBetaSku.IsEnabled = CheckBoxIncludeSku.IsEnabled = false;
                CheckBoxIncludeBetaSku.IsChecked = CheckBoxIncludeSku.IsChecked = false;
            }
            else
            {
                CheckBoxIncludeBetaSku.IsEnabled = CheckBoxIncludeSku.IsEnabled = true;
            }

            Update();
        }

        private void CheckBoxIncludeSku_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Debug.Assert(CheckBoxIncludeSku.IsChecked != null, "CheckBoxIncludeSku.IsChecked != null");
#endif
            CheckBoxIncludeBetaSku.IsChecked = CheckBoxIncludeBetaSku.IsEnabled = CheckBoxIncludeSku.IsChecked.Value;
            Update();
        }
    }
}
