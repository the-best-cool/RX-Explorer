﻿using Microsoft.Toolkit.Uwp.Notifications;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Input;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using ProgressBar = Microsoft.UI.Xaml.Controls.ProgressBar;
using TabViewItem = Microsoft.UI.Xaml.Controls.TabViewItem;

namespace RX_Explorer
{
    public sealed partial class ThisPC : Page
    {
        private WeakReference<TabViewItem> WeakToTabItem;

        private QuickStartItem CurrentSelectedItem;
        private int LockResource;

        public ThisPC()
        {
            InitializeComponent();
            Loaded += ThisPC_Loaded;
        }

        private void ThisPC_Loaded(object sender, RoutedEventArgs e)
        {
            if (ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] is bool Enable)
            {
                LeftSideCol.Width = Enable ? new GridLength(2.5, GridUnitType.Star) : new GridLength(0);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = true;

                LeftSideCol.Width = new GridLength(2.5, GridUnitType.Star);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e?.Parameter is WeakReference<TabViewItem> Parameters)
            {
                WeakToTabItem = Parameters;

                if (WeakToTabItem.TryGetTarget(out TabViewItem Tab))
                {
                    Tab.Header = Globalization.GetString("MainPage_PageDictionary_ThisPC_Label");
                }
            }
        }

        private void DeviceGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
            }
            else
            {
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;

                if (AnimationController.Current.IsEnableAnimation)
                {
                    ProgressBar ProBar = args.ItemContainer.FindChildOfType<ProgressBar>();
                    Storyboard Story = new Storyboard();
                    DoubleAnimation Animation = new DoubleAnimation()
                    {
                        To = (args.Item as HardDeviceInfo).Percent,
                        From = 0,
                        EnableDependentAnimation = true,
                        EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut },
                        Duration = new TimeSpan(0, 0, 0, 0, 800)
                    };
                    Storyboard.SetTarget(Animation, ProBar);
                    Storyboard.SetTargetProperty(Animation, "Value");
                    Story.Children.Add(Animation);
                    Story.Begin();
                }
            }
        }

        private void LibraryGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
            }
            else
            {
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
            }
        }

        private void ItemContainer_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!SettingControl.IsDoubleClickEnable)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is object Item)
                {
                    if (Item is LibraryFolder)
                    {
                        LibraryGrid.SelectedItem = Item;
                        DeviceGrid.SelectedIndex = -1;
                    }
                    else if (Item is HardDeviceInfo)
                    {
                        DeviceGrid.SelectedItem = Item;
                        LibraryGrid.SelectedIndex = -1;
                    }
                }
            }
        }

        public async Task OpenTargetFolder(StorageFolder Folder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Argument could not be null");
            }

            try
            {
                if (string.IsNullOrEmpty(Folder.Path))
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
                        await Launcher.LaunchFolderAsync(Folder);
                    }
                }
                else
                {
                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        Frame.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string>(WeakToTabItem, Folder.Path), new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        Frame.Navigate(typeof(FileControl), new Tuple<WeakReference<TabViewItem>, string>(WeakToTabItem, Folder.Path), new SuppressNavigationTransitionInfo());
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when entering device");
            }
        }

        private async void DeviceGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if ((e.OriginalSource as FrameworkElement)?.DataContext is HardDeviceInfo Device)
            {
                if (Device.IsLockedByBitlocker)
                {
                Retry:
                    BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        await FullTrustProcessController.Current.RunAsync("powershell.exe", true, true, true, "-Command", $"$BitlockerSecureString = ConvertTo-SecureString '{Dialog.Password}' -AsPlainText -Force;", $"Unlock-BitLocker -MountPoint '{Device.Folder.Path}' -Password $BitlockerSecureString").ConfigureAwait(true);

                        StorageFolder DeviceFolder = await StorageFolder.GetFolderFromPathAsync(Device.Folder.Path);

                        BasicProperties Properties = await DeviceFolder.GetBasicPropertiesAsync();
                        IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                        HardDeviceInfo NewDevice = new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, Device.DriveType);

                        if (!NewDevice.IsLockedByBitlocker)
                        {
                            int Index = CommonAccessCollection.HardDeviceList.IndexOf(Device);
                            CommonAccessCollection.HardDeviceList.Remove(Device);
                            CommonAccessCollection.HardDeviceList.Insert(Index, NewDevice);
                        }
                        else
                        {
                            QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await UnlockFailedDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                goto Retry;
                            }
                        }
                    }
                }
                else
                {
                    await OpenTargetFolder(Device.Folder).ConfigureAwait(false);
                }
            }
        }

        private async void LibraryGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            DeviceGrid.SelectedIndex = -1;

            if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Library)
            {
                await OpenTargetFolder(Library.Folder).ConfigureAwait(false);
            }
        }

        private async void QuickStartGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is QuickStartItem Item)
            {
                if ((sender as GridView).Name == nameof(QuickStartGridView))
                {
                    Uri Ur = new Uri(Item.Protocol);

                    if (Ur.IsFile)
                    {
                        if (WIN_Native_API.CheckExist(Item.Protocol))
                        {
                        Retry:
                            try
                            {
                                if (Path.GetExtension(Item.Protocol).ToLower() == ".msc")
                                {
                                    await FullTrustProcessController.Current.RunAsync("powershell.exe", false, true, false, "-Command", Item.Protocol).ConfigureAwait(true);
                                }
                                else
                                {
                                    await FullTrustProcessController.Current.RunAsync(Item.Protocol).ConfigureAwait(true);
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                                    {
                                        goto Retry;
                                    }
                                    else
                                    {
                                        QueueContentDialog ErrorDialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }
                            }
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_ApplicationNotFound_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                    else
                    {
                        await Launcher.LaunchUriAsync(Ur);
                    }
                }
                else
                {
                    await Launcher.LaunchUriAsync(new Uri(Item.Protocol));
                }
            }
        }

        private async void AppDelete_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSelectedItem != null)
            {
                CommonAccessCollection.QuickStartList.Remove(CurrentSelectedItem);
                await SQLite.Current.DeleteQuickStartItemAsync(CurrentSelectedItem).ConfigureAwait(false);
            }
        }

        private async void AppEdit_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSelectedItem != null)
            {
                QuickStartModifiedDialog dialog = new QuickStartModifiedDialog(QuickStartType.UpdateApp, CurrentSelectedItem);
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void WebEdit_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSelectedItem != null)
            {
                QuickStartModifiedDialog dialog = new QuickStartModifiedDialog(QuickStartType.UpdateWeb, CurrentSelectedItem);
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void WebDelete_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSelectedItem != null)
            {
                CommonAccessCollection.HotWebList.Remove(CurrentSelectedItem);
                await SQLite.Current.DeleteQuickStartItemAsync(CurrentSelectedItem).ConfigureAwait(false);
            }
        }

        private void QuickStartGridView_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((sender as GridView).Name == nameof(QuickStartGridView))
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is QuickStartItem Item)
                    {
                        CurrentSelectedItem = Item;

                        QuickStartGridView.ContextFlyout = AppFlyout;
                    }
                    else
                    {
                        QuickStartGridView.ContextFlyout = AppEmptyFlyout;
                    }
                }
                else
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is QuickStartItem Item)
                    {
                        CurrentSelectedItem = Item;

                        WebGridView.ContextFlyout = WebFlyout;
                    }
                    else
                    {
                        WebGridView.ContextFlyout = WebEmptyFlyout;
                    }
                }
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            DeviceInfoDialog Dialog = new DeviceInfoDialog(DeviceGrid.SelectedItem as HardDeviceInfo);
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private void DeviceGrid_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is HardDeviceInfo Context)
                {
                    DeviceGrid.SelectedItem = Context;

                    if (Context.IsLockedByBitlocker)
                    {
                        DeviceGrid.ContextFlyout = BitlockerDeviceFlyout;
                    }
                    else
                    {
                        DeviceGrid.ContextFlyout = Context.DriveType == DriveType.Removable ? PortableDeviceFlyout : DeviceFlyout;
                    }
                }
                else
                {
                    DeviceGrid.SelectedIndex = -1;
                    DeviceGrid.ContextFlyout = EmptyFlyout;
                }
            }
        }

        private async void OpenDevice_Click(object sender, RoutedEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if (DeviceGrid.SelectedItem is HardDeviceInfo Device)
            {
                if (Device.IsLockedByBitlocker)
                {
                Retry:
                    BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        await FullTrustProcessController.Current.RunAsync("powershell.exe", true, true, true, "-Command", $"$BitlockerSecureString = ConvertTo-SecureString '{Dialog.Password}' -AsPlainText -Force;", $"Unlock-BitLocker -MountPoint '{Device.Folder.Path}' -Password $BitlockerSecureString").ConfigureAwait(true);

                        StorageFolder DeviceFolder = await StorageFolder.GetFolderFromPathAsync(Device.Folder.Path);

                        BasicProperties Properties = await DeviceFolder.GetBasicPropertiesAsync();
                        IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                        HardDeviceInfo NewDevice = new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, Device.DriveType);

                        if (!NewDevice.IsLockedByBitlocker)
                        {
                            int Index = CommonAccessCollection.HardDeviceList.IndexOf(Device);
                            CommonAccessCollection.HardDeviceList.Remove(Device);
                            CommonAccessCollection.HardDeviceList.Insert(Index, NewDevice);
                        }
                        else
                        {
                            QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await UnlockFailedDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                goto Retry;
                            }
                        }
                    }
                }
                else
                {
                    await OpenTargetFolder(Device.Folder).ConfigureAwait(false);
                }
            }
        }

        private void DeviceGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                DeviceGrid.SelectedIndex = -1;
            }
            else
            {
                LibraryGrid.SelectedIndex = -1;
            }
        }

        private void LibraryGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                LibraryGrid.SelectedIndex = -1;
            }
            else
            {
                DeviceGrid.SelectedIndex = -1;
            }
        }

        private void Grid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DeviceGrid.SelectedIndex = -1;
            LibraryGrid.SelectedIndex = -1;
        }

        private void LibraryGrid_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Context)
                {
                    LibraryGrid.SelectedItem = Context;
                    LibraryGrid.ContextFlyout = LibraryFlyout;
                }
                else
                {
                    LibraryGrid.ContextFlyout = LibraryEmptyFlyout;
                }
            }
        }

        private async void OpenLibrary_Click(object sender, RoutedEventArgs e)
        {
            DeviceGrid.SelectedIndex = -1;

            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                await OpenTargetFolder(Library.Folder).ConfigureAwait(false);
            }
        }

        private async void RemovePin_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                CommonAccessCollection.LibraryFolderList.Remove(Library);
                await SQLite.Current.DeleteLibraryAsync(Library.Folder.Path).ConfigureAwait(false);
                await JumpListController.Current.RemoveItem(JumpListGroup.Library, Library.Folder).ConfigureAwait(false);
            }
        }

        private async void LibraryProperties_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                PropertyDialog Dialog = new PropertyDialog(FileSystemStorageItemBase.Open(Library.Folder.Path, ItemFilters.Folder));
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        public async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref LockResource, 1) == 0)
            {
                try
                {
                    CommonAccessCollection.HardDeviceList.Clear();

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

                    foreach (DeviceInformation Device in await DeviceInformation.FindAllAsync(StorageDevice.GetDeviceSelector()))
                    {
                        try
                        {
                            StorageFolder DeviceFolder = StorageDevice.FromId(Device.Id);

                            if (CommonAccessCollection.HardDeviceList.All((Item) => (string.IsNullOrEmpty(Item.Folder.Path) || string.IsNullOrEmpty(DeviceFolder.Path)) ? Item.Folder.Name != DeviceFolder.Name : Item.Folder.Path != DeviceFolder.Path))
                            {
                                BasicProperties Properties = await DeviceFolder.GetBasicPropertiesAsync();
                                IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                                if (PropertiesRetrieve["System.Capacity"] is ulong && PropertiesRetrieve["System.FreeSpace"] is ulong)
                                {
                                    CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, DriveType.Removable));
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
                                            CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), InnerPropertiesRetrieve, DriveType.Removable));
                                        }
                                        else
                                        {
                                            CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, DriveType.Removable));
                                        }
                                    }
                                    else
                                    {
                                        CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, DriveType.Removable));
                                    }
                                }
                            }
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
                }
                finally
                {
                    _ = Interlocked.Exchange(ref LockResource, 0);
                }
            }
        }

        private async void DeviceGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if (!SettingControl.IsDoubleClickEnable && e.ClickedItem is HardDeviceInfo Device)
            {
                if (Device.IsLockedByBitlocker)
                {
                Retry:
                    BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        await FullTrustProcessController.Current.RunAsync("powershell.exe", true, true, true, "-Command", $"$BitlockerSecureString = ConvertTo-SecureString '{Dialog.Password}' -AsPlainText -Force;", $"Unlock-BitLocker -MountPoint '{Device.Folder.Path}' -Password $BitlockerSecureString").ConfigureAwait(true);

                        StorageFolder DeviceFolder = await StorageFolder.GetFolderFromPathAsync(Device.Folder.Path);

                        BasicProperties Properties = await DeviceFolder.GetBasicPropertiesAsync();
                        IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                        HardDeviceInfo NewDevice = new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, Device.DriveType);

                        if (!NewDevice.IsLockedByBitlocker)
                        {
                            int Index = CommonAccessCollection.HardDeviceList.IndexOf(Device);
                            CommonAccessCollection.HardDeviceList.Remove(Device);
                            CommonAccessCollection.HardDeviceList.Insert(Index, NewDevice);
                        }
                        else
                        {
                            QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await UnlockFailedDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                goto Retry;
                            }
                        }
                    }
                }
                else
                {
                    await OpenTargetFolder(Device.Folder).ConfigureAwait(false);
                }
            }
        }

        private async void LibraryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            DeviceGrid.SelectedIndex = -1;

            if (!SettingControl.IsDoubleClickEnable && e.ClickedItem is LibraryFolder Library)
            {
                await OpenTargetFolder(Library.Folder).ConfigureAwait(false);
            }
        }

        private async void AddDevice_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add("*");

            StorageFolder Device = await Picker.PickSingleFolderAsync();

            if (Device != null)
            {
                if (Device.Path.Equals(Path.GetPathRoot(Device.Path), StringComparison.OrdinalIgnoreCase) && DriveInfo.GetDrives().Where((Drive) => Drive.DriveType == DriveType.Fixed || Drive.DriveType == DriveType.Removable).Any((Item) => Item.RootDirectory.FullName == Device.Path))
                {
                    if (CommonAccessCollection.HardDeviceList.All((Item) => Item.Folder.Path != Device.Path))
                    {
                        BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                        IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });
                        CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, new DriveInfo(Device.Path).DriveType));
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_DeviceExist_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_DeviceSelectError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_TipTitle")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private void QuickStartGridView_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if ((sender as GridView).Name == nameof(QuickStartGridView))
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is QuickStartItem Item)
                    {
                        CurrentSelectedItem = Item;

                        QuickStartGridView.ContextFlyout = AppFlyout;
                    }
                    else
                    {
                        QuickStartGridView.ContextFlyout = AppEmptyFlyout;
                    }
                }
                else
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is QuickStartItem Item)
                    {
                        CurrentSelectedItem = Item;

                        WebGridView.ContextFlyout = WebFlyout;
                    }
                    else
                    {
                        WebGridView.ContextFlyout = WebEmptyFlyout;
                    }
                }
            }
        }

        private void LibraryGrid_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Context)
                {
                    LibraryGrid.SelectedItem = Context;
                    LibraryGrid.ContextFlyout = LibraryFlyout;
                }
                else
                {
                    LibraryGrid.ContextFlyout = null;
                }
            }
        }

        private void DeviceGrid_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is HardDeviceInfo Context)
                {
                    DeviceGrid.SelectedItem = Context;
                    DeviceGrid.ContextFlyout = Context.DriveType == DriveType.Removable ? PortableDeviceFlyout : DeviceFlyout;
                }
                else
                {
                    DeviceGrid.SelectedIndex = -1;
                    DeviceGrid.ContextFlyout = EmptyFlyout;
                }
            }
        }

        private void LibraryExpander_Collapsed(object sender, EventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;
        }

        private void DeviceExpander_Collapsed(object sender, EventArgs e)
        {
            DeviceGrid.SelectedIndex = -1;
        }

        private async void EjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceGrid.SelectedItem is HardDeviceInfo Item)
            {
                if (string.IsNullOrEmpty(Item.Folder.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueContentDialog_UnableToEject_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                }
                else
                {
                    foreach (TabViewItem Tab in TabViewContainer.ThisPage.TabViewControl.TabItems.Select((Obj) => Obj as TabViewItem).Where((Tab) => Tab.Content is Frame frame && CommonAccessCollection.FrameFileControlDic.ContainsKey(frame) && Path.GetPathRoot(CommonAccessCollection.FrameFileControlDic[frame].CurrentFolder?.Path) == Item.Folder.Path).ToArray())
                    {
                        await TabViewContainer.ThisPage.CleanUpAndRemoveTabItem(Tab).ConfigureAwait(true);
                    }

                    if (await FullTrustProcessController.Current.EjectPortableDevice(Item.Folder.Path).ConfigureAwait(true))
                    {
                        ShowEjectNotification();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueContentDialog_UnableToEject_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        private void ShowEjectNotification()
        {
            ToastNotificationManager.History.Remove("MergeVideoNotification");

            ToastContentBuilder Builder = new ToastContentBuilder()
                                          .SetToastScenario(ToastScenario.Default)
                                          .AddToastActivationInfo("Transcode", ToastActivationType.Foreground)
                                          .AddText(Globalization.GetString("Eject_Toast_Text_1"))
                                          .AddText(Globalization.GetString("Eject_Toast_Text_2"))
                                          .AddText(Globalization.GetString("Eject_Toast_Text_3"));

            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));
        }

        private async void AddQuickStartWeb_Click(object sender, RoutedEventArgs e)
        {
            QuickStartModifiedDialog dialog = new QuickStartModifiedDialog(QuickStartType.WebSite);
            _ = await dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void AddQuickStartApp_Click(object sender, RoutedEventArgs e)
        {
            QuickStartModifiedDialog dialog = new QuickStartModifiedDialog(QuickStartType.Application);
            _ = await dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void WebGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            await SQLite.Current.DeleteQuickStartItemAsync(QuickStartType.WebSite).ConfigureAwait(true);

            foreach (QuickStartItem Item in CommonAccessCollection.HotWebList)
            {
                await SQLite.Current.SetQuickStartItemAsync(Item.DisplayName, Item.RelativePath, Item.Protocol, QuickStartType.WebSite).ConfigureAwait(true);
            }
        }

        private async void QuickStartGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            await SQLite.Current.DeleteQuickStartItemAsync(QuickStartType.Application).ConfigureAwait(true);

            foreach (QuickStartItem Item in CommonAccessCollection.QuickStartList)
            {
                await SQLite.Current.SetQuickStartItemAsync(Item.DisplayName, Item.RelativePath, Item.Protocol, QuickStartType.Application).ConfigureAwait(true);
            }
        }

        private async void LibraryGrid_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            await SQLite.Current.ClearTableAsync("Library").ConfigureAwait(true);

            foreach (LibraryFolder Item in CommonAccessCollection.LibraryFolderList)
            {
                await SQLite.Current.SetLibraryPathAsync(Item.Folder.Path, Item.Type).ConfigureAwait(true);
            }
        }

        private async void AddLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                if (CommonAccessCollection.LibraryFolderList.Any((Library) => Library.Folder.Path.Equals(Folder.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_RepeatAddToHomePage_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    CommonAccessCollection.LibraryFolderList.Add(new LibraryFolder(Folder, await Folder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibraryType.UserCustom));
                    await SQLite.Current.SetLibraryPathAsync(Folder.Path, LibraryType.UserCustom).ConfigureAwait(false);
                    await JumpListController.Current.AddItem(JumpListGroup.Library, Folder.Path).ConfigureAwait(false);
                }
            }
        }

        private async void UnlockBitlocker_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceGrid.SelectedItem is HardDeviceInfo Device)
            {
            Retry:
                BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    await FullTrustProcessController.Current.RunAsync("powershell.exe", true, true, true, "-Command", $"$BitlockerSecureString = ConvertTo-SecureString '{Dialog.Password}' -AsPlainText -Force;", $"Unlock-BitLocker -MountPoint '{Device.Folder.Path}' -Password $BitlockerSecureString").ConfigureAwait(true);

                    StorageFolder DeviceFolder = await StorageFolder.GetFolderFromPathAsync(Device.Folder.Path);

                    BasicProperties Properties = await DeviceFolder.GetBasicPropertiesAsync();
                    IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                    HardDeviceInfo NewDevice = new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, Device.DriveType);

                    if (!NewDevice.IsLockedByBitlocker)
                    {
                        int Index = CommonAccessCollection.HardDeviceList.IndexOf(Device);
                        CommonAccessCollection.HardDeviceList.Remove(Device);
                        CommonAccessCollection.HardDeviceList.Insert(Index, NewDevice);
                    }
                    else
                    {
                        QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await UnlockFailedDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            goto Retry;
                        }
                    }
                }
            }
        }

        private void GridView_PreviewKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                e.Handled = true;
            }
        }
    }
}
