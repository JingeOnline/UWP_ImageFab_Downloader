using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace UWP_ImageFab_Downloader
{
    public sealed partial class ContentDialog_DownloadFinish : ContentDialog
    {
        public ObservableCollection<Picture> PictureFailList { get; set; }
        public string ResutText { get; set; }
        public ObservableCollection<Album> InvalidAlbums { get; set; }


        public ContentDialog_DownloadFinish(MainPage mainPage)
        {
            this.InitializeComponent();
            PictureFailList = mainPage.PictureFailCollection;
            InvalidAlbums = mainPage.InvalidAlbums;

            ResutText = "Download images count: " + mainPage.DownloadImagesCount + "\n" +
    "Download images size: " + mainPage.DownloadImagesSize + "\n" +
    "Images fail to download: " + mainPage.PictureFailCollection.Count + "\n" +
    "Albums fail to download: " + mainPage.InvalidAlbums.Count;
            Grid_FailList.Visibility = needShow(PictureFailList.Count);
            Grid_AlbumFailList.Visibility = needShow(InvalidAlbums.Count);
        }
        public Visibility needShow(int fail)
        {
            if (fail == 0)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }


    }
}
