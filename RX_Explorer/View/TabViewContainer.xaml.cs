﻿using HtmlAgilityPack;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using SymbolIconSource = Microsoft.UI.Xaml.Controls.SymbolIconSource;
using TabView = Microsoft.UI.Xaml.Controls.TabView;
using TabViewTabCloseRequestedEventArgs = Microsoft.UI.Xaml.Controls.TabViewTabCloseRequestedEventArgs;

namespace RX_Explorer
{
    public sealed partial class TabViewContainer : Page
    {
        private int LockResource;

        public static Frame CurrentTabNavigation { get; private set; }

        private readonly DeviceWatcher PortalDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);

        public static TabViewContainer ThisPage { get; private set; }

        public TabViewContainer()
        {
            InitializeComponent();
            ThisPage = this;
            Loaded += TabViewContainer_Loaded;
            PortalDeviceWatcher.Added += PortalDeviceWatcher_Added;
            PortalDeviceWatcher.Removed += PortalDeviceWatcher_Removed;
            Application.Current.Resuming += Current_Resuming;
            Application.Current.Suspending += Current_Suspending;
            CoreWindow.GetForCurrentThread().PointerPressed += TabViewContainer_PointerPressed;
            CoreWindow.GetForCurrentThread().KeyDown += TabViewContainer_KeyDown;
        }

        private async void TabViewContainer_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (!QueueContentDialog.IsRunningOrWaiting)
            {
                CoreVirtualKeyStates CtrlState = sender.GetKeyState(VirtualKey.Control);

                switch (args.VirtualKey)
                {
                    case VirtualKey.W when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            if (TabViewControl.SelectedItem is TabViewItem Tab)
                            {
                                args.Handled = true;

                                await CleanUpAndRemoveTabItem(Tab).ConfigureAwait(true);
                            }

                            return;
                        }
                }

                if (CurrentTabNavigation?.Content is ThisPC PC)
                {
                    switch (args.VirtualKey)
                    {
                        case VirtualKey.Space when SettingControl.IsQuicklookAvailable && SettingControl.IsQuicklookEnable:
                            {
                                if (PC.DeviceGrid.SelectedItem is HardDeviceInfo Device)
                                {
                                    await FullTrustProcessController.Current.ViewWithQuicklookAsync(Device.Folder.Path).ConfigureAwait(false);
                                }
                                else if (PC.LibraryGrid.SelectedItem is LibraryFolder Library)
                                {
                                    await FullTrustProcessController.Current.ViewWithQuicklookAsync(Library.Folder.Path).ConfigureAwait(false);
                                }
                                break;
                            }
                        case VirtualKey.Enter:
                            {
                                if (PC.DeviceGrid.SelectedItem is HardDeviceInfo Device)
                                {
                                    if (string.IsNullOrEmpty(Device.Folder.Path))
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                            Content = Globalization.GetString("QueueDialog_MTP_CouldNotAccess_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                        };

                                        if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                                        {
                                            await Launcher.LaunchFolderAsync(Device.Folder);
                                        }
                                    }
                                    else
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            CurrentTabNavigation.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string>(new WeakReference<TabViewItem>(TabViewControl.SelectedItem as TabViewItem), Device.Folder.Path), new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            CurrentTabNavigation.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string>(new WeakReference<TabViewItem>(TabViewControl.SelectedItem as TabViewItem), Device.Folder.Path), new SuppressNavigationTransitionInfo());
                                        }
                                    }

                                    args.Handled = true;
                                }
                                else if (PC.LibraryGrid.SelectedItem is LibraryFolder Library)
                                {
                                    if (AnimationController.Current.IsEnableAnimation)
                                    {
                                        CurrentTabNavigation.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string>(new WeakReference<TabViewItem>(TabViewControl.SelectedItem as TabViewItem), Library.Folder.Path), new DrillInNavigationTransitionInfo());
                                    }
                                    else
                                    {
                                        CurrentTabNavigation.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string>(new WeakReference<TabViewItem>(TabViewControl.SelectedItem as TabViewItem), Library.Folder.Path), new SuppressNavigationTransitionInfo());
                                    }

                                    args.Handled = true;
                                }

                                break;
                            }
                        case VirtualKey.F5:
                            {
                                PC.Refresh_Click(null, null);

                                args.Handled = true;

                                break;
                            }
                    }
                }
            }
        }

        private void TabViewContainer_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            bool BackButtonPressed = args.CurrentPoint.Properties.IsXButton1Pressed;
            bool ForwardButtonPressed = args.CurrentPoint.Properties.IsXButton2Pressed;

            if (CurrentTabNavigation?.Content is FileControl Control)
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;

                    if (!QueueContentDialog.IsRunningOrWaiting)
                    {
                        if (Control.GoBackRecord.IsEnabled)
                        {
                            Control.GoBackRecord_Click(null, null);
                        }
                        else
                        {
                            MainPage.ThisPage.NavView_BackRequested(null, null);
                        }
                    }
                }
                else if (ForwardButtonPressed)
                {
                    args.Handled = true;

                    if (!QueueContentDialog.IsRunningOrWaiting && Control.GoForwardRecord.IsEnabled)
                    {
                        Control.GoForwardRecord_Click(null, null);
                    }
                }
            }
            else
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;

                    MainPage.ThisPage.NavView_BackRequested(null, null);
                }
                else if (ForwardButtonPressed)
                {
                    args.Handled = true;
                }
            }
        }

        public void CreateNewTabAndOpenTargetFolder(string Path, int? InsertIndex = null)
        {
            int Index = InsertIndex ?? (TabViewControl?.TabItems.Count ?? 0);

            try
            {
                if (string.IsNullOrWhiteSpace(Path))
                {
                    if (CreateNewTab() is TabViewItem Item)
                    {
                        TabViewControl.TabItems.Insert(Index, Item);
                        TabViewControl.UpdateLayout();
                        TabViewControl.SelectedItem = Item;
                    }
                }
                else
                {
                    if (CreateNewTab(Path) is TabViewItem Item)
                    {
                        TabViewControl.TabItems.Insert(Index, Item);
                        TabViewControl.UpdateLayout();
                        TabViewControl.SelectedItem = Item;
                    }
                }
            }
            catch (Exception ex)
            {
                if (CreateNewTab() is TabViewItem Item)
                {
                    TabViewControl.TabItems.Insert(Index, Item);
                    TabViewControl.UpdateLayout();
                    TabViewControl.SelectedItem = Item;
                }

                LogTracer.Log(ex, "Error happened when try to create a new tab");
            }
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            if (PortalDeviceWatcher != null && (PortalDeviceWatcher.Status == DeviceWatcherStatus.Started || PortalDeviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
            {
                PortalDeviceWatcher.Stop();
            }
        }

        private void Current_Resuming(object sender, object e)
        {
            switch (PortalDeviceWatcher.Status)
            {
                case DeviceWatcherStatus.Created:
                case DeviceWatcherStatus.Aborted:
                case DeviceWatcherStatus.Stopped:
                    {
                        PortalDeviceWatcher?.Start();
                        break;
                    }
            }
        }

        private async void PortalDeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            try
            {
                List<string> AllBaseDevice = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed)
                                                                  .Select((Info) => Info.RootDirectory.FullName).ToList();

                List<StorageFolder> PortableDevice = new List<StorageFolder>();

                foreach (DeviceInformation Device in await DeviceInformation.FindAllAsync(StorageDevice.GetDeviceSelector()))
                {
                    try
                    {
                        PortableDevice.Add(StorageDevice.FromId(Device.Id));
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Error happened when get storagefolder from {Device.Name}");
                    }
                }

                foreach (string PortDevice in AllBaseDevice.Where((Path) => PortableDevice.Any((Item) => Item.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))))
                {
                    AllBaseDevice.Remove(PortDevice);
                }

                List<HardDeviceInfo> OneStepDeviceList = CommonAccessCollection.HardDeviceList.Where((Item) => !AllBaseDevice.Contains(Item.Folder.Path)).ToList();
                List<HardDeviceInfo> TwoStepDeviceList = OneStepDeviceList.Where((RemoveItem) => PortableDevice.All((Item) => Item.Name != RemoveItem.Folder.Name)).ToList();

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    foreach (HardDeviceInfo Device in TwoStepDeviceList)
                    {
                        foreach (TabViewItem Tab in TabViewControl.TabItems.OfType<TabViewItem>().Where((Tab) => Tab.Content is Frame frame && CommonAccessCollection.FrameFileControlDic.TryGetValue(frame, out FileControl Value) && Path.GetPathRoot(Value.CurrentFolder?.Path) == Device.Folder?.Path).ToArray())
                        {
                            await CleanUpAndRemoveTabItem(Tab).ConfigureAwait(true);
                        }

                        CommonAccessCollection.HardDeviceList.Remove(Device);
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error happened when remove device from HardDeviceList");
            }
        }

        private async void PortalDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            try
            {
                StorageFolder DeviceFolder = StorageDevice.FromId(args.Id);

                if (CommonAccessCollection.HardDeviceList.All((Device) => (string.IsNullOrEmpty(Device.Folder.Path) || string.IsNullOrEmpty(DeviceFolder.Path)) ? Device.Folder.Name != DeviceFolder.Name : Device.Folder.Path != DeviceFolder.Path))
                {
                    BasicProperties Properties = await DeviceFolder.GetBasicPropertiesAsync();
                    IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                    if (PropertiesRetrieve["System.Capacity"] is ulong && PropertiesRetrieve["System.FreeSpace"] is ulong)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, DriveType.Removable));
                        });
                    }
                    else
                    {
                        IReadOnlyList<IStorageItem> InnerItemList = await DeviceFolder.GetItemsAsync(0, 2);

                        if (InnerItemList.Count == 1 && InnerItemList[0] is StorageFolder InnerFolder)
                        {
                            BasicProperties InnerProperties = await InnerFolder.GetBasicPropertiesAsync();
                            IDictionary<string, object> InnerPropertiesRetrieve = await InnerProperties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                            if (InnerPropertiesRetrieve["System.Capacity"] is ulong && InnerPropertiesRetrieve["System.FreeSpace"] is ulong)
                            {
                                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                                {
                                    CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), InnerPropertiesRetrieve, DriveType.Removable));
                                });
                            }
                            else
                            {
                                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                                {
                                    CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, DriveType.Removable));
                                });
                            }
                        }
                        else
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                            {
                                CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, DriveType.Removable));
                            });
                        }
                    }
                }
            }
            catch
            {
                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("DisableDeviceFailTip"))
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = $"{Globalization.GetString("QueueDialog_AddDeviceFail_Content")} \"{args.Name}\"",
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_DoNotTip"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            ApplicationData.Current.LocalSettings.Values["DisableDeviceFailTip"] = true;
                        }
                    });
                }
            }
        }

        private async void TabViewContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= TabViewContainer_Loaded;

            if (MainPage.ThisPage.IsPathActivate)
            {
                CreateNewTabAndOpenTargetFolder(MainPage.ThisPage.ActivatePath);

                MainPage.ThisPage.IsPathActivate = false;
            }
            else
            {
                CreateNewTabAndOpenTargetFolder(string.Empty);
            }

            try
            {
                foreach (KeyValuePair<QuickStartType, QuickStartItem> Item in await SQLite.Current.GetQuickStartItemAsync().ConfigureAwait(true))
                {
                    if (Item.Key == QuickStartType.Application)
                    {
                        CommonAccessCollection.QuickStartList.Add(Item.Value);
                    }
                    else
                    {
                        CommonAccessCollection.HotWebList.Add(Item.Value);
                    }
                }

                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("IsLibraryInitialized"))
                {
                    try
                    {
                        IReadOnlyList<User> UserList = await User.FindAllAsync();

                        UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                 ? UserDataPaths.GetForUser(CurrentUser)
                                                 : UserDataPaths.GetDefault();
                        try
                        {
                            if (!string.IsNullOrEmpty(DataPath.Downloads))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Downloads, LibraryType.Downloads).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Desktop))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Desktop, LibraryType.Desktop).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Videos))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Videos, LibraryType.Videos).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Pictures))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Pictures, LibraryType.Pictures).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Documents))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Documents, LibraryType.Document).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Music))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Music, LibraryType.Music).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OneDrive")))
                            {
                                await SQLite.Current.SetLibraryPathAsync(Environment.GetEnvironmentVariable("OneDrive"), LibraryType.OneDrive).ConfigureAwait(true);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "An error was threw when getting library folder (In initialize)");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An error was threw when try to get 'UserDataPath' (In initialize)");

                        string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        if (!string.IsNullOrEmpty(DesktopPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(DesktopPath, LibraryType.Desktop).ConfigureAwait(true);
                        }

                        string VideoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                        if (!string.IsNullOrEmpty(VideoPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(VideoPath, LibraryType.Videos).ConfigureAwait(true);
                        }

                        string PicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                        if (!string.IsNullOrEmpty(PicturesPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(PicturesPath, LibraryType.Pictures).ConfigureAwait(true);
                        }

                        string DocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        if (!string.IsNullOrEmpty(DocumentsPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(DocumentsPath, LibraryType.Document).ConfigureAwait(true);
                        }

                        string MusicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                        if (!string.IsNullOrEmpty(MusicPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(MusicPath, LibraryType.Music).ConfigureAwait(true);
                        }

                        string OneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
                        if (!string.IsNullOrEmpty(OneDrivePath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(OneDrivePath, LibraryType.OneDrive).ConfigureAwait(true);
                        }
                    }
                    finally
                    {
                        ApplicationData.Current.LocalSettings.Values["IsLibraryInitialized"] = true;
                    }
                }
                else
                {
                    try
                    {
                        IReadOnlyList<User> UserList = await User.FindAllAsync();

                        UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                 ? UserDataPaths.GetForUser(CurrentUser)
                                                 : UserDataPaths.GetDefault();
                        try
                        {
                            if (!string.IsNullOrEmpty(DataPath.Downloads))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Downloads, LibraryType.Downloads).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Desktop))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Desktop, LibraryType.Desktop).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Videos))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Videos, LibraryType.Videos).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Pictures))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Pictures, LibraryType.Pictures).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Documents))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Documents, LibraryType.Document).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Music))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Music, LibraryType.Music).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OneDrive")))
                            {
                                await SQLite.Current.UpdateLibraryAsync(Environment.GetEnvironmentVariable("OneDrive"), LibraryType.OneDrive).ConfigureAwait(true);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "An error was threw when getting library folder (Not in initialize)");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An error was threw when try to get 'UserDataPath' (Not in initialize)");

                        string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        if (!string.IsNullOrEmpty(DesktopPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(DesktopPath, LibraryType.Desktop).ConfigureAwait(true);
                        }

                        string VideoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                        if (!string.IsNullOrEmpty(VideoPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(VideoPath, LibraryType.Videos).ConfigureAwait(true);
                        }

                        string PicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                        if (!string.IsNullOrEmpty(PicturesPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(PicturesPath, LibraryType.Pictures).ConfigureAwait(true);
                        }

                        string DocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        if (!string.IsNullOrEmpty(DocumentsPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(DocumentsPath, LibraryType.Document).ConfigureAwait(true);
                        }

                        string MusicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                        if (!string.IsNullOrEmpty(MusicPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(MusicPath, LibraryType.Music).ConfigureAwait(true);
                        }

                        string OneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
                        if (!string.IsNullOrEmpty(OneDrivePath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(OneDrivePath, LibraryType.OneDrive).ConfigureAwait(true);
                        }
                    }
                }

                Queue<string> ErrorList = new Queue<string>();
                foreach ((string, LibraryType) Library in await SQLite.Current.GetLibraryPathAsync().ConfigureAwait(true))
                {
                    try
                    {
                        StorageFolder PinFolder = await StorageFolder.GetFolderFromPathAsync(Library.Item1);
                        BitmapImage Thumbnail = await PinFolder.GetThumbnailBitmapAsync().ConfigureAwait(true);
                        CommonAccessCollection.LibraryFolderList.Add(new LibraryFolder(PinFolder, Thumbnail, Library.Item2));
                    }
                    catch (Exception)
                    {
                        ErrorList.Enqueue(Library.Item1);
                        await SQLite.Current.DeleteLibraryAsync(Library.Item1).ConfigureAwait(true);
                    }
                }

                await JumpListController.Current.AddItem(JumpListGroup.Library, CommonAccessCollection.LibraryFolderList.Where((Library) => Library.Type == LibraryType.UserCustom).Select((Library) => Library.Folder.Path).ToArray()).ConfigureAwait(true);

                bool AccessError = false;
                foreach (DriveInfo Drive in DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable)
                                                                 .Where((NewItem) => CommonAccessCollection.HardDeviceList.All((Item) => Item.Folder.Path != NewItem.RootDirectory.FullName)))
                {
                    try
                    {
                        StorageFolder Device = await StorageFolder.GetFolderFromPathAsync(Drive.RootDirectory.FullName);

                        BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                        IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                        CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, Drive.DriveType));
                    }
                    catch
                    {
                        AccessError = true;
                    }
                }

                if (AccessError && !ApplicationData.Current.LocalSettings.Values.ContainsKey("DisableAccessErrorTip"))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_DeviceHideForError_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_DoNotTip"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        ApplicationData.Current.LocalSettings.Values["DisableAccessErrorTip"] = true;
                    }
                }

                switch (PortalDeviceWatcher.Status)
                {
                    case DeviceWatcherStatus.Created:
                    case DeviceWatcherStatus.Aborted:
                    case DeviceWatcherStatus.Stopped:
                        {
                            PortalDeviceWatcher?.Start();
                            break;
                        }
                }

                if (ErrorList.Count > 0)
                {
                    StringBuilder Builder = new StringBuilder();

                    while (ErrorList.TryDequeue(out string ErrorMessage))
                    {
                        Builder.AppendLine($"   {ErrorMessage}");
                    }

                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_PinFolderNotFound_Content") + Builder.ToString(),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                LogTracer.LeadToBlueScreen(ex);
            }
        }

        private async void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            await CleanUpAndRemoveTabItem(args.Tab).ConfigureAwait(true);
        }

        private void TabViewControl_AddTabButtonClick(TabView sender, object args)
        {
            if (CreateNewTab() is TabViewItem Item)
            {
                sender.TabItems.Add(Item);
                sender.UpdateLayout();
                sender.SelectedItem = Item;
            }
        }

        private TabViewItem CreateNewTab(string PathForNewTab = null)
        {
            if (Interlocked.Exchange(ref LockResource, 1) == 0)
            {
                try
                {
                    Frame frame = new Frame();

                    TabViewItem Item = new TabViewItem
                    {
                        IconSource = new SymbolIconSource { Symbol = Symbol.Document },
                        AllowDrop = true,
                        IsDoubleTapEnabled = true
                    };
                    Item.DragEnter += Item_DragEnter;
                    Item.PointerPressed += Item_PointerPressed;
                    Item.DoubleTapped += Item_DoubleTapped;

                    if (!string.IsNullOrEmpty(PathForNewTab) && WIN_Native_API.CheckExist(PathForNewTab))
                    {
                        frame.Navigate(typeof(ThisPC), new WeakReference<TabViewItem>(Item), new SuppressNavigationTransitionInfo());

                        if (AnimationController.Current.IsEnableAnimation)
                        {
                            frame.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string>(new WeakReference<TabViewItem>(Item), PathForNewTab), new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            frame.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string>(new WeakReference<TabViewItem>(Item), PathForNewTab), new SuppressNavigationTransitionInfo());
                        }
                    }
                    else
                    {
                        frame.Navigate(typeof(ThisPC), new WeakReference<TabViewItem>(Item), new SuppressNavigationTransitionInfo());
                    }

                    Item.Content = frame;

                    return Item;
                }
                finally
                {
                    _ = Interlocked.Exchange(ref LockResource, 0);
                }
            }
            else
            {
                return null;
            }
        }

        private async void Item_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (sender is TabViewItem Tab)
            {
                await CleanUpAndRemoveTabItem(Tab).ConfigureAwait(false);
            }
        }

        private async void Item_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed)
            {
                if (sender is TabViewItem Tab)
                {
                    await CleanUpAndRemoveTabItem(Tab).ConfigureAwait(false);
                }
            }
        }

        private async void Item_DragEnter(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    if (e.OriginalSource is TabViewItem Item)
                    {
                        TabViewControl.SelectedItem = Item;
                    }
                }
                else if (e.DataView.Contains(StandardDataFormats.Html))
                {
                    string Html = await e.DataView.GetHtmlFormatAsync();
                    string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                    HtmlDocument Document = new HtmlDocument();
                    Document.LoadHtml(Fragment);
                    HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                    if (HeadNode?.InnerText == "RX-Explorer-TabItem")
                    {
                        e.AcceptedOperation = DataPackageOperation.Link;
                    }
                }
            }
            catch
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void TabViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (Frame Nav in TabViewControl.TabItems.Select((Item) => (Item as TabViewItem).Content as Frame))
            {
                Nav.Navigated -= Nav_Navigated;
            }

            if (TabViewControl.SelectedItem is TabViewItem Item)
            {
                CurrentTabNavigation = Item.Content as Frame;
                CurrentTabNavigation.Navigated += Nav_Navigated;

                if (CurrentTabNavigation.Content is ThisPC)
                {
                    TaskBarController.SetText(null);
                }
                else
                {
                    TaskBarController.SetText(Convert.ToString(Item.Header));
                }

                MainPage.ThisPage.NavView.IsBackEnabled = (MainPage.ThisPage.SettingControl?.IsOpened).GetValueOrDefault() || CurrentTabNavigation.CanGoBack;
            }
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            MainPage.ThisPage.NavView.IsBackEnabled = CurrentTabNavigation.CanGoBack;
        }

        private void TabViewControl_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
        {
            StringBuilder Builder = new StringBuilder("<head>RX-Explorer-TabItem</head>");

            if (args.Tab.Content is Frame frame)
            {
                if (frame.Content is ThisPC)
                {
                    Builder.Append("<p>ThisPC||</p>");
                }
                else
                {
                    if (CommonAccessCollection.FrameFileControlDic.ContainsKey(frame))
                    {
                        Builder.Append($"<p>FileControl||{CommonAccessCollection.FrameFileControlDic[frame].CurrentFolder.Path}</p>");
                    }
                    else
                    {
                        args.Cancel = true;
                    }
                }
            }

            args.Data.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(Builder.ToString()));
        }

        private async void TabViewControl_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
        {
            if (args.DropResult == DataPackageOperation.Link)
            {
                await CleanUpAndRemoveTabItem(args.Tab).ConfigureAwait(false);
            }
        }

        private async void TabViewControl_TabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
        {
            if (sender.TabItems.Count > 1)
            {
                if (args.Tab.Content is Frame frame)
                {
                    if (frame.Content is ThisPC)
                    {
                        await CleanUpAndRemoveTabItem(args.Tab).ConfigureAwait(true);
                        await Launcher.LaunchUriAsync(new Uri($"rx-explorer:"));
                    }
                    else if (CommonAccessCollection.FrameFileControlDic.TryGetValue(frame, out FileControl Control))
                    {
                        Uri NewWindowActivationUri = new Uri($"rx-explorer:{Uri.EscapeDataString(Control.CurrentFolder.Path)}");
                        await CleanUpAndRemoveTabItem(args.Tab).ConfigureAwait(true);
                        await Launcher.LaunchUriAsync(NewWindowActivationUri);
                    }
                }
            }
        }

        private async void TabViewControl_TabStripDragOver(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.Html))
                {
                    string Html = await e.DataView.GetHtmlFormatAsync();
                    string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                    HtmlDocument Document = new HtmlDocument();
                    Document.LoadHtml(Fragment);
                    HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                    if (HeadNode?.InnerText == "RX-Explorer-TabItem")
                    {
                        e.AcceptedOperation = DataPackageOperation.Link;
                        e.Handled = true;
                    }
                }
            }
            catch
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void TabViewControl_TabStripDrop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.Html))
                {
                    string Html = await e.DataView.GetHtmlFormatAsync();
                    string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                    HtmlDocument Document = new HtmlDocument();
                    Document.LoadHtml(Fragment);
                    HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                    if (HeadNode?.InnerText == "RX-Explorer-TabItem")
                    {
                        HtmlNode BodyNode = Document.DocumentNode.SelectSingleNode("/p");
                        string[] Split = BodyNode.InnerText.Split("||", StringSplitOptions.RemoveEmptyEntries);

                        int InsertIndex = TabViewControl.TabItems.Count;

                        for (int i = 0; i < TabViewControl.TabItems.Count; i++)
                        {
                            TabViewItem Item = TabViewControl.ContainerFromIndex(i) as TabViewItem;

                            Windows.Foundation.Point Position = e.GetPosition(Item);

                            if (Position.X < Item.ActualWidth)
                            {
                                if (Position.X < Item.ActualWidth / 2)
                                {
                                    InsertIndex = i;
                                    break;
                                }
                                else
                                {
                                    InsertIndex = i + 1;
                                    break;
                                }
                            }
                        }

                        switch (Split[0])
                        {
                            case "ThisPC":
                                {
                                    CreateNewTabAndOpenTargetFolder(string.Empty, InsertIndex);
                                    break;
                                }
                            case "FileControl":
                                {
                                    CreateNewTabAndOpenTargetFolder(Split[1], InsertIndex);
                                    break;
                                }
                        }

                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Error happened when try to drop a tab");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        public async Task CleanUpAndRemoveTabItem(TabViewItem Tab)
        {
            if (Tab == null)
            {
                throw new ArgumentNullException(nameof(Tab), "Argument could not be null");
            }

            if (Tab.Content is Frame frame)
            {
                while (frame.CanGoBack)
                {
                    if (frame.Content is FileControl Control)
                    {
                        if (Control.Presenter.ListViewControl?.Header is ListViewHeaderController Controller)
                        {
                            Controller.Dispose();
                        }

                        Control.Dispose();

                        break;
                    }
                    else
                    {
                        frame.GoBack();
                    }
                }

                CommonAccessCollection.FrameFileControlDic.Remove(frame);
            }

            Tab.DragEnter -= Item_DragEnter;
            Tab.PointerPressed -= Item_PointerPressed;
            Tab.DoubleTapped -= Item_DoubleTapped;
            Tab.Content = null;

            TabViewControl.TabItems.Remove(Tab);

            if (TabViewControl.TabItems.Count == 0)
            {
                await ApplicationView.GetForCurrentView().TryConsolidateAsync();
            }
        }
    }
}
