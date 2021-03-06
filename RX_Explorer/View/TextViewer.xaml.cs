﻿using RX_Explorer.Class;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace RX_Explorer
{
    public sealed partial class TextViewer : Page
    {
        private FileSystemStorageItemBase TextFile;

        private Encoding CurrentEncoding;

        public TextViewer()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private void Initialize()
        {
            EncodingInfo[] EncodingCollection = Encoding.GetEncodings();
            EncodingProfile.ItemsSource = EncodingCollection;

            Encoding DetectedEncoding = DetectEncodingFromFile();

            if (EncodingCollection.FirstOrDefault((Enco) => Enco.CodePage == DetectedEncoding.CodePage) is EncodingInfo Info)
            {
                EncodingProfile.SelectedItem = Info;
            }
            else
            {
                EncodingProfile.SelectedItem = EncodingCollection.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
            }
        }

        private Encoding DetectEncodingFromFile()
        {
            using (FileStream DetectStream = TextFile.GetFileStreamFromFile(AccessMode.Read))
            using (StreamReader Reader = new StreamReader(DetectStream, Encoding.Default, true))
            {
                Reader.Read();

                return Reader.CurrentEncoding;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e?.Parameter is FileSystemStorageItemBase Parameters)
            {
                TextFile = Parameters;
                Title.Text = TextFile.Name;

                Initialize();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            TextFile = null;
            Text.Text = string.Empty;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurrentEncoding != null)
                {
                    try
                    {
                        using (FileStream Stream = FileSystemStorageItemBase.Create(TextFile.Path, StorageItemTypes.File, CreateOption.ReplaceExisting).GetFileStreamFromFile(AccessMode.Write))
                        using (StreamWriter Writer = new StreamWriter(Stream, CurrentEncoding))
                        {
                            await Writer.WriteAsync(Text.Text).ConfigureAwait(true);
                        }
                    }
                    catch
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CouldReadWriteFile_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync().ConfigureAwait(true);
                    }

                    Frame.GoBack();
                }
                else
                {
                    InvalidTip.IsOpen = true;
                }
            }
            catch
            {
                InvalidTip.IsOpen = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private async void EncodingProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EncodingProfile.SelectedItem is EncodingInfo Info)
            {
                CurrentEncoding = Info.GetEncoding();
                await LoadTextWithEncoding(CurrentEncoding).ConfigureAwait(false);
            }
        }

        private void EncodingProfile_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            try
            {
                if (Encoding.GetEncodings().FirstOrDefault((Enco) => Enco.DisplayName == args.Text) is EncodingInfo ExistEnco)
                {
                    if (CurrentEncoding != ExistEnco.GetEncoding())
                    {
                        CurrentEncoding = ExistEnco.GetEncoding();
                        _ = LoadTextWithEncoding(CurrentEncoding);
                    }
                }
                else
                {
                    if (int.TryParse(args.Text, out int CodePage))
                    {
                        CurrentEncoding = Encoding.GetEncoding(CodePage);
                        _ = LoadTextWithEncoding(CurrentEncoding);
                    }
                    else
                    {
                        CurrentEncoding = Encoding.GetEncoding(args.Text);
                        _ = LoadTextWithEncoding(CurrentEncoding);
                    }
                }

                args.Handled = false;
            }
            catch
            {
                args.Handled = true;
                InvalidTip.IsOpen = true;
            }
        }

        private async Task LoadTextWithEncoding(Encoding Enco)
        {
            LoadingControl.IsLoading = true;

            try
            {
                using (FileStream Stream = TextFile.GetFileStreamFromFile(AccessMode.Read))
                using (StreamReader Reader = new StreamReader(Stream, Enco, false))
                {
                    Text.Text = await Reader.ReadToEndAsync().ConfigureAwait(true);
                }
            }
            catch
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CouldReadWriteFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync().ConfigureAwait(true);
            }

            await Task.Delay(500).ConfigureAwait(true);
            LoadingControl.IsLoading = false;
        }
    }
}
