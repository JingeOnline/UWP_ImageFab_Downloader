﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace UWP_ImageFab_Downloader
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        //允许由于写入错误，而等待并重新执行的次数
        private int tryReWrite = 5;

        //用户输入的URL
        private string urlInput;
        public string UrlInput
        {
            get
            {
                return urlInput;
            }
            set
            {
                urlInput = value;
                OnPropertyChanged("UrlInput");
            }
        }
        //URL对应的相册列表
        public ObservableCollection<Album> AlbumCollection = new ObservableCollection<Album>();
        //URL解析失败相册列表
        public ObservableCollection<Album> InvalidAlbums = new ObservableCollection<Album>();

        //当前下载的图片文件名称
        public string CurrentImageName { get; set; }
        //当前已下载文件的总大小
        private long downloadImagesSize = 0;
        public string DownloadImagesSize
        {
            get
            {
                if (downloadImagesSize > 1024 * 1024)
                {
                    return downloadImagesSize / 1024 / 1024 + " MB";
                }
                else
                {
                    return downloadImagesSize / 1024 + " KB";
                }
            }
            set
            {
                OnPropertyChanged("DownloadImagesSize");
            }
        }
        //当前已下载的图片总数量
        private int downloadImagesCount = 0;
        public int DownloadImagesCount
        {
            get { return downloadImagesCount; }
            set
            {
                downloadImagesCount = value;
                OnPropertyChanged("DownloadImagesCount");
            }
        }
        //下载失败的文件List
        public ObservableCollection<Picture> PictureFailCollection = new ObservableCollection<Picture>();

        private StorageFolder downloadFolder;
        public StorageFolder DownloadFolder
        {
            get
            {
                return downloadFolder;
            }
            set
            {
                downloadFolder = value;
                OnPropertyChanged("DownloadFolder");
                localSettings.Values["DownloadFolderPath"] = value.Path;
            }
        }

        private string startButtonText = "Start Download";
        public string StartButtonText
        {
            get { return startButtonText; }
            set
            {
                startButtonText = value;
                OnPropertyChanged("StartButtonText");
            }
        }
        private Symbol startButtonSymbol = Symbol.Download;
        public Symbol StartButtonSymbol
        {
            get { return startButtonSymbol; }
            set
            {
                startButtonSymbol = value;
                OnPropertyChanged("StartButtonSymbol");
            }
        }
        private bool isPausing = false;
        public bool IsPausing
        {
            get { return isPausing; }
            set
            {
                isPausing = value;
                setButtonTextAndSymbol();
            }
        }
        private bool isDownloadStart = false;
        public bool IsDownloadStart
        {
            get { return isDownloadStart; }
            set
            {
                isDownloadStart = value;
                setButtonTextAndSymbol();
            }
        }

        private void setButtonTextAndSymbol()
        {
            if (!isDownloadStart)
            {
                StartButtonText = "Start Download";
                StartButtonSymbol = Symbol.Download;
            }
            else
            {
                if (!isPausing)
                {
                    StartButtonText = "Pause";
                    StartButtonSymbol = Symbol.Clock;
                }
                else
                {
                    StartButtonText = "Resume";
                    StartButtonSymbol = Symbol.Download;
                }
            }
        }

        //应用程序的setting
        private static Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        //是否播放下载完成的提示音
        private bool isSoundOn = true;
        public bool IsSoundOn
        {
            get { return isSoundOn; }
            set
            {
                isSoundOn = value;
                OnPropertyChanged("IsSoundOn");
                localSettings.Values["IsSoundOn"] = value.ToString();
            }
        }
        //是否下载到独立的文件夹
        private bool needIndividualFolder = true;
        public bool NeedIndividualFolder
        {
            get { return needIndividualFolder; }
            set
            {
                needIndividualFolder = value;
                OnPropertyChanged("NeedIndividualFolder");
                if (NeedSaveDownloadFolder)
                {
                    localSettings.Values["NeedIndividualFolder"] = value.ToString();
                }
            }
        }
        //是否保存下载路径
        private bool needSaveDownloadFolder = true;

        public bool NeedSaveDownloadFolder
        {
            get { return needSaveDownloadFolder; }
            set
            {
                needSaveDownloadFolder = value;
                OnPropertyChanged("NeedSaveDownloadFolder");
                localSettings.Values["NeedSaveDownloadFolder"] = value.ToString();
                setDefaultDownloadFolder();
            }
        }

        //是否允许把粘贴内容自动添加到下载列表
        private bool enablePaste = false;
        public bool EnablePaste
        {
            get { return enablePaste; }
            set
            {
                enablePaste = value;
                OnPropertyChanged("EnablePaste");
                localSettings.Values["EnablePaste"] = value.ToString();
            }
        }
        //是否播放教程
        private bool isFirstRun = true;
        public bool IsFirstRun
        {
            get { return isFirstRun; }
            set
            {
                isFirstRun = false;
                OnPropertyChanged("IsFirstRun");
                localSettings.Values["IsFirstRun"] = value.ToString();
            }
        }
        //UI上显示的链接
        private string webLink = @"https://www.iconfinder.com/free_icons";
        public string WebLink
        {
            get { return webLink; }
            set { webLink = value; OnPropertyChanged("WebLink"); }
        }
        //UI上显示的文字提示
        private string introductionMessage = "Support download icons from:";

        public string IntroductionMessage
        {
            get { return introductionMessage; }
            set { introductionMessage = value; OnPropertyChanged("IntroductionMessage"); }
        }
        //UI上显示的Logo文字
        private string logoText = "ImageFab Downloader";
        public string LogoText
        {
            get { return logoText; }
            set { logoText = value; OnPropertyChanged("LogoText"); }
        }



        //后台值变动，前台能随时更新UI
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public MainPage()
        {
            this.InitializeComponent();
            myInitialize();
        }

        public async void myInitialize()
        {
            await getAppSetting();
            //string s=await htmlDownloadAsync("https://www.imagefap.com/gallery.php?gid=8758423");
            if (IsFirstRun)
            {
                showTutorial();
                IsFirstRun = false;
            }
            await getTheRemoteSetting();
        }

        //用户添加一条URL
        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(UrlInput))
            {
                TeachingTip_NullUrlInput.IsOpen = true;
                return;
            }
            AlbumCollection.Add(new Album(UrlInput));
            UrlInput = string.Empty;
        }

        //删除一个还未开始下载的相册
        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Album album = button.DataContext as Album;
            AlbumCollection.Remove(album);
        }

        //用户选择下载路径
        private async void Button_SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            Windows.Storage.StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                // Application now has read/write access to all contents in the picked folder
                // (including other sub-folder contents)
                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                DownloadFolder = folder;
            }
            else
            {
                //DownloadFolderPath = "Operation cancelled.";
            }
        }

        //用户点击“下载”和“暂停”
        private void Button_RunOrPause_Click(object sender, RoutedEventArgs e)
        {
            //还没有开始下载
            if (!IsDownloadStart)
            {
                //如果下载列表为空，弹出提示对话
                if (AlbumCollection.Count == 0)
                {
                    TeachingTip_NullUrlInput.IsOpen = true;
                    return;
                }
                //开始下载
                else
                {
                    IsDownloadStart = true;
                    startDonwload();
                }

            }
            //如果已经开始下载,暂停/恢复
            else
            {
                IsPausing = !IsPausing;
            }
        }



        //开始主流程，解析和下载流程
        private async void startDonwload()
        {
            for (int i = 0; i < AlbumCollection.Count; i++)
            {
                //去图标网站下载图标
                if (AlbumCollection[i].AlbumUrlUserInput.Contains("www.iconfinder.com"))
                {
                    AlbumCollection[i].AlbumMessage = "Downloading...";
                    await downloadIconsFromWeb(AlbumCollection[i]);
                    AlbumCollection[i].IsDownloaded = true;
                    AlbumCollection[i].AlbumMessage = "Complete";
                }
                else
                {
                    //ListView_1.SelectedItem = AlbumCollection[i];
                    AlbumCollection[i] = await getImagePagesUrlsFromAlbum(AlbumCollection[i]);
                    AlbumCollection[i].AlbumMessage = "Downloading...";
                    if (AlbumCollection[i].IsUrlValid == false)
                    {
                        AlbumCollection[i].AlbumMessage = "Invalid URL";
                        InvalidAlbums.Add(AlbumCollection[i]);
                        continue;
                    }
                    //AlbumCollection[i] = album;
                    await donwloadAlbumObject(AlbumCollection[i]);
                    AlbumCollection[i].AlbumMessage = "Complete";
                }

            }
            //等待两秒钟，等所有Task都完成，等UI的数据都更新完。
            await Task.Delay(2000);
            //下载完成播放提示音
            if (IsSoundOn)
            {
                var player = new MediaPlayer();
                player.Source = MediaSource.CreateFromUri(new Uri("ms-winsoundevent:Notification.Looping.Alarm3"));
                player.Play();
            }
            ContentDialog_DownloadFinish dialog = new ContentDialog_DownloadFinish(this);
            await dialog.ShowAsync();
            resetData();
        }


        //传入初始的相册URL，找到所有的ImagePage的url，返回album
        //TODO:判断URL无效
        private async Task<Album> getImagePagesUrlsFromAlbum(Album album)
        {

            //去掉url头尾的空格
            string url = album.AlbumUrlUserInput.Trim();
            //通过域名过滤无效链接
            Match matchDomain = Regex.Match(url, @"https://www.imagefap.com/");
            if (string.IsNullOrEmpty(matchDomain.Value))
            {
                album.IsUrlValid = false;
                return album;
            }
            //如果链接是相册的第二页即以后
            if (url.Contains("&page="))
            {
                int index = url.LastIndexOf("&page=");
                url = url.Remove(index);
            }

            //把当前链接替换成“一页显示所有图片”的链接
            if (url.Contains("&view="))
            {
                int index = url.LastIndexOf("&view=");
                url = url.Remove(index);
            }
            url += "&view=2";

            try
            {
                string htmlAlbumPage = await htmlDownloadAsync(url);
                if (htmlAlbumPage == null)
                {
                    album.IsUrlValid = false;
                    return album;
                }
                album.ImagePageUrlList = searchPageUrlInAlbumPage(htmlAlbumPage);
                album.AlbumName = searchAlbumNameInAlbumPage(htmlAlbumPage);
                return album;
            }
            catch
            {
                album.IsUrlValid = false;
                return album;
            }

        }

        //在Album Page中找到每张图片的Image Page地址
        private List<String> searchPageUrlInAlbumPage(string html)
        {
            string pattern = @"/photo/[^\f\n\r\t\v>\u0022]*";
            MatchCollection urls = Regex.Matches(html, pattern);
            List<string> urlList = new List<string>();
            foreach (Match item in urls)
            {
                string pattern3 = @"amp;";
                String url = Regex.Replace(item.ToString(), pattern3, "");
                url = "https://www.imagefap.com" + url;
                urlList.Add(url);
            }
            return urlList;
        }
        //在Album Page中找到相册的名称
        private string searchAlbumNameInAlbumPage(string html)
        {
            string pattern1 = "<title>[^']+</title>";
            Match match1 = Regex.Match(html, pattern1);
            string pattern2 = "[^><]+&";
            Match match2 = Regex.Match(match1.Value, pattern2);
            string name = match2.Value;
            if (name.Contains(" Porn Pics"))
            {
                int index = name.LastIndexOf(" Porn Pics");
                name = match2.Value.Remove(index);
            }

            //排除windows不允许创建文件夹名称时使用的字符
            name = name.Replace("/", "_");
            name = name.Replace("\\", "_");
            name = name.Replace("<", "_");
            name = name.Replace(">", "_");
            name = name.Replace(":", "_");
            name = name.Replace("*", "_");
            name = name.Replace("?", "_");
            name = name.Replace("|", "_");
            name = name.Replace("\"", "_");
            name = name.Replace(".", "_");

            //去掉首位空格，否则在创建文件夹的时候，windows不允许文件夹的末尾出现空格
            name = name.Trim();
            return name;
        }


        //下载一个解析好的Album对象，把每张图片存入本地
        private async Task donwloadAlbumObject(Album album)
        {
            //判断是否需要为相册创建单独的文件夹
            StorageFolder saveFolder = DownloadFolder;
            if (NeedIndividualFolder)
            {
                saveFolder = await DownloadFolder.CreateFolderAsync(album.AlbumName, CreationCollisionOption.GenerateUniqueName);
            }


            int picNum = 0;
            foreach (string imagePageUrl in album.ImagePageUrlList)
            {
                picNum++;
                Picture picture = await findPictureInImagePage(album.AlbumName, picNum, imagePageUrl);
                if (picture == null)
                {
                    PictureFailCollection.Add(picture);
                    continue;
                }
                album.PictureList.Add(picture);
                //让程序暂停，但已经开始的Task还是会完成下载。
                while (IsPausing)
                {
                    await Task.Delay(1000);
                }

                imageDownload(picture, saveFolder);

            }
            album.IsDownloaded = true;
            //album.DownloadStateIcon = Symbol.Accept;
        }

        //下载ImagePage页面，找到Image的url，返回Picture对象
        private async Task<Picture> findPictureInImagePage(string albumName, int picNum, string pageUrl)
        {
            try
            {
                string html = await htmlDownloadAsync(pageUrl);
                string pattern = @"https://cdn.imagefap.com/images/full[^\f\n\r\t\v>\u0022]*";
                Match match = Regex.Match(html, pattern);
                string imageUrl = match.ToString();

                //查找图片的文件类型（gif,jpg,jpeg,png）
                string patternFileExtention = "[.](gif|jpg|jpeg|png|bmp)";
                string fileExtention = Regex.Match(imageUrl, patternFileExtention).ToString();

                Picture picture = new Picture();
                picture.PictureFileName = albumName + "_" + picNum + fileExtention;
                picture.PictureUrl = imageUrl;
                picture.PicturePageUrl = pageUrl;
                return picture;
            }
            catch
            {
                return null;
            }

        }

        //下载网页HTML文本
        private async Task<string> htmlDownloadAsync(string url)
        {

            //HTTP下载
            var uri = new System.Uri(url);
            using (var httpClient = new Windows.Web.Http.HttpClient())
            {
                // Always catch network exceptions for async methods
                try
                {
                    string result = await httpClient.GetStringAsync(uri);
                    return result;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        //下载图片并写入到本地
        private async void imageDownload(Picture picture, StorageFolder saveFolder)
        {

            try
            {
                Uri uri = new System.Uri(picture.PictureUrl);
                HttpClient client = new HttpClient();

                byte[] buffer = await client.GetByteArrayAsync(uri);
                //创建新文件，如果该文件存在则自动在末尾追加一个Unique名称。
                StorageFile file = await saveFolder.CreateFileAsync(picture.PictureFileName, options: CreationCollisionOption.GenerateUniqueName);
                using (Stream stream = await file.OpenStreamForWriteAsync())
                {
                    stream.Write(buffer, 0, buffer.Length);
                }
                DownloadImagesCount++;
                //DownloadImagesSize += buffer.Length;
                downloadImagesSize += buffer.Length;
                //让绑定的ImagesDownloadedSize属性强制刷新
                DownloadImagesSize = "";
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                PictureFailCollection.Add(picture);
            }
            //处理由于文件写入失败而引发的异常，莫名其妙经常会出现
            //“The process cannot access the file because it is being used by another process.”的异常
            catch (System.IO.FileLoadException) when (tryReWrite >= 0)
            {
                //PictureFailCollection.Add(picture);
                await Task.Delay(1000);
                tryReWrite--;
                imageDownload(picture, saveFolder);
            }
            catch (System.IO.FileLoadException e)
            {
                await new MessageDialog(picture.PictureFileName + " occur an error.\n" + e.Message, "Exception").ShowAsync();
            }
            catch (System.UriFormatException)
            {
                PictureFailCollection.Add(picture);
            }
            catch (System.IO.IOException)
            {
                await new MessageDialog( "Your device is disconnected from Internet.","Exception").ShowAsync();
            }
        }


        private async void writeStringToFile(string fileName, string content)
        {
            //创建新文件，如果该文件存在则自动在末尾追加一个Unique名称。
            StorageFile file = await DownloadFolder.CreateFileAsync(fileName, options: CreationCollisionOption.GenerateUniqueName);
            //把文字写入文件
            await FileIO.WriteTextAsync(file, content); ;
        }

        //重置程序的所有统计数据和UI
        public void resetData()
        {
            AlbumCollection.Clear();
            InvalidAlbums.Clear();
            PictureFailCollection.Clear();
            //ToggleButton_RunOrPause.IsChecked = false;
            IsDownloadStart = false;
            IsPausing = false;
            downloadImagesSize = 0;
            DownloadImagesSize = "";
            DownloadImagesCount = 0;
            tryReWrite = 5;
            this.Bindings.Update();
        }

        //获得app的setting
        private async Task getAppSetting()
        {
            //获取用户对是否保存下载路径的设置
            object needSavePath = localSettings.Values["NeedSaveDownloadFolder"];
            if (needSavePath != null)
            {
                NeedSaveDownloadFolder = needSavePath.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            //获取用户下载的文件夹路径
            if (!NeedSaveDownloadFolder)
            {
                setDefaultDownloadFolder();
            }
            else
            {
                object downloadFolderPath = localSettings.Values["DownloadFolderPath"];
                if (downloadFolderPath != null)
                {
                    try
                    {
                        DownloadFolder = await StorageFolder.GetFolderFromPathAsync(downloadFolderPath.ToString());
                    }
                    catch
                    {
                        //当用户之前设置的文件夹已经不存在
                        setDefaultDownloadFolder();
                    }
                }
                else
                {
                    setDefaultDownloadFolder();
                }
            }

            //获取用户对下载完成提示音的设置
            object isOn = localSettings.Values["IsSoundOn"];
            if (isOn != null)
            {
                IsSoundOn = isOn.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            //获取用户对是否需要为每个相册建立独立文件夹的设置
            object needIndividualFolder = localSettings.Values["NeedIndividualFolder"];
            if (needIndividualFolder != null)
            {
                NeedIndividualFolder = needIndividualFolder.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            //获取用户对是否允许自动粘贴的设置
            object ePaste = localSettings.Values["EnablePaste"];
            if (ePaste != null)
            {
                EnablePaste = ePaste.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            //获取用户是否是第一次运行该程序
            object isFirstTime = localSettings.Values["IsFirstRun"];
            if (isFirstTime != null)
            {
                IsFirstRun = isFirstTime.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }

        //设置下载路径到默认的“图片相册”
        private async void setDefaultDownloadFolder()
        {
            var pictureLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
            //downloadFolder= pictureLibrary.SaveFolder;
            DownloadFolder = pictureLibrary.SaveFolder;
        }

        //用户点击CommandBar中的Setting按钮
        private void AppBarButton_Setting_Click(object sender, RoutedEventArgs e)
        {
            SplitView_Setting.IsPaneOpen = !SplitView_Setting.IsPaneOpen;
        }

        //用户粘贴URL到TextBox,粘贴失败会播放音效
        private async void TextBox_UrlInput_Paste(object sender, TextControlPasteEventArgs e)
        {
            //AlbumCollection.Add(new Album(e.ToString));
            if (EnablePaste)
            {
                var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (dataPackageView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                {
                    try
                    {
                        var text = await dataPackageView.GetTextAsync();
                        AlbumCollection.Add(new Album(text));
                    }
                    catch
                    {
                        var player = new MediaPlayer();
                        player.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Sounds/Windows_1.wav"));
                        player.Play();
                    }
                }

                TextBox textbox = sender as TextBox;
                textbox.Text = string.Empty;
            }

        }
        private void AppBarButton_Tutorial_Click(object sender, RoutedEventArgs e)
        {
            showTutorial();
        }

        private void showTutorial()
        {
            TeachingTip_EnterUrl.IsOpen = true;
            TeachingTip_EnterUrl.Closed += TeachingTip_EnterUrl_Closed;
        }

        private void TeachingTip_EnterUrl_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            //throw new NotImplementedException();
            TeachingTip_AddButton.IsOpen = true;
            TeachingTip_AddButton.Closed += TeachingTip_AddButton_Closed;
        }

        private void TeachingTip_AddButton_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            //throw new NotImplementedException();
            TeachingTip_DownloadTaskList.IsOpen = true;
            TeachingTip_DownloadTaskList.Closed += TeachingTip_DownloadTaskList_Closed;
        }

        private void TeachingTip_DownloadTaskList_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            //throw new NotImplementedException();
            TeachingTip_SelectFolderButton.IsOpen = true;
            TeachingTip_SelectFolderButton.Closed += TeachingTip_SelectFolderButton_Closed;
        }

        private void TeachingTip_SelectFolderButton_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            //throw new NotImplementedException();
            TeachingTip_StartDownloadButton.IsOpen = true;
            TeachingTip_StartDownloadButton.Closed += TeachingTip_StartDownloadButton_Closed;
        }

        private void TeachingTip_StartDownloadButton_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            //throw new NotImplementedException();
            TeachingTip_Setting.IsOpen = true;
            TeachingTip_Setting.Closed += TeachingTip_Setting_Closed;
        }

        private void TeachingTip_Setting_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            //throw new NotImplementedException();
            TeachingTip_Tutorial.IsOpen = true;
        }

        //获取Github Api中的设置信息
        private async Task getTheRemoteSetting()
        {
            try
            {
                //await Task.Delay(5000);
                string url = "https://raw.githubusercontent.com/JingeOnline/AppSetting_UWP_ImageFapDownloader/master/AppRemoteSetting.txt";
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true
                };
                var respond = await httpClient.GetAsync(url);
                string result = await respond.Content.ReadAsStringAsync();
                string[] text = result.Split("\n");
                //await new MessageDialog(s[1]+s[0], "对话框标题").ShowAsync();
                LogoText = text[0];
                IntroductionMessage = text[1];
                WebLink = text[2];
            }
            catch
            {
            }

        }
        //去图标网站下载图标
        private async Task downloadIconsFromWeb(Album album)
        {
            string htmlPage = await htmlDownloadAsync(album.AlbumUrlUserInput);
            string pattern1 = "srcset=\"[^\"]+";
            MatchCollection matches = Regex.Matches(htmlPage, pattern1);
            string pattern2 = "[a-zA-z]+://[^\\s]*";
            //List<string> imageUrls = new List<string>();
            foreach (Match match in matches)
            {
                MatchCollection matches1 = Regex.Matches(match.Value, pattern2);
                foreach (Match match_1 in matches1)
                {
                    if (match_1.Value.Contains("/previews/"))
                    {

                    }
                    else
                    {
                        string[] temp = match_1.Value.Split("/");
                        string fileName = temp[temp.Length - 1];
                        string pictureUrl = match_1.Value;
                        album.PictureList.Add(
                            new Picture()
                            {
                                PictureUrl = pictureUrl,
                                PictureFileName = fileName,
                                PicturePageUrl = pictureUrl,
                            }
                            );
                    }
                }
            }

            album.AlbumName = searchFolderNameInHtml(htmlPage);
            //判断是否需要为相册创建单独的文件夹
            StorageFolder saveFolder = DownloadFolder;
            if (NeedIndividualFolder)
            {
                saveFolder = await DownloadFolder.CreateFolderAsync(album.AlbumName, CreationCollisionOption.GenerateUniqueName);
            }

            foreach (Picture picture in album.PictureList)
            {
                imageDownload(picture, saveFolder);
            }
        }

        private string searchFolderNameInHtml(string html)
        {
            string pattern1 = "<title>[^']+</title>";
            Match match1 = Regex.Match(html, pattern1);
            string name = match1.Value;
            //排除windows不允许创建文件夹名称时使用的字符
            name = name.Replace("/", "_");
            name = name.Replace("\\", "_");
            name = name.Replace("<", "_");
            name = name.Replace(">", "_");
            name = name.Replace(":", "_");
            name = name.Replace("*", "_");
            name = name.Replace("?", "_");
            name = name.Replace("|", "_");
            name = name.Replace("\"", "_");
            name = name.Replace(".", "_");

            if (name.Length >= 255)
            {
                name = name.Remove(253);
            }


            //去掉首位空格，否则在创建文件夹的时候，windows不允许文件夹的末尾出现空格
            name = name.Trim();
            return name;
        }
    }
}
