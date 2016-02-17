using System;
using System.Collections.Generic;
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
using Windows.Storage;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage.FileProperties;
using AlbumCoverMatchGame.Models;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Store;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AlbumCoverMatchGame
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public LicenseInformation AppLicenseInformation { get; set; }
        private ObservableCollection<Song> songs;
        private ObservableCollection<StorageFile> allSongs;
        bool _playingMusic = false;
        int _round = 0;
        int _totalScore = 0;

        public MainPage()
        {
            this.InitializeComponent();

            songs = new ObservableCollection<Song>();
        }

        private async Task retrieveFilesInFolders(ObservableCollection<StorageFile> list,StorageFolder parent)
        {
            foreach (var item in await parent.GetFilesAsync())
            {
                if (item.FileType == ".mp3")
                    list.Add(item);
            }

            foreach (var item in await parent.GetFoldersAsync())
            {
                await retrieveFilesInFolders(list, item);
            }
        }

        private async Task<List<StorageFile>> pickRandomSongs(ObservableCollection<StorageFile> allSongs)
        {
            Random random = new Random();
            var songsCount = allSongs.Count();
            var randomSongs = new List<StorageFile>();

            while (randomSongs.Count<10)
            {
                var randomNum = random.Next(songsCount);
                var randomSong = allSongs[randomNum];

                //Find random songs BUT:
                //1. Don't pick the same song twice!
                //2. Don't pick a song from an album that I've already picked

                MusicProperties randomSongMusicProperties = await randomSong.Properties.GetMusicPropertiesAsync();

                bool isDuplicate = false;
                foreach (var song in randomSongs)
                {
                    MusicProperties songMusicProperties = await song.Properties.GetMusicPropertiesAsync();
                    if (String.IsNullOrEmpty(randomSongMusicProperties.Album) || randomSongMusicProperties.Album == songMusicProperties.Album)
                        isDuplicate = true;
                }
                if (!isDuplicate)
                    randomSongs.Add(randomSong);
            }
            return randomSongs;
        }

        private async Task populateSongList(List<StorageFile> files)
        {
            int id = 0;
            foreach (var file in files)
            {
                MusicProperties songProperties = await file.Properties.GetMusicPropertiesAsync();
                StorageItemThumbnail currentThumb = await file.GetThumbnailAsync(ThumbnailMode.MusicView,200,ThumbnailOptions.UseCurrentScale);
                var song = new Song();
                var albumCover = new BitmapImage();
                albumCover.SetSource(currentThumb);
                song.Id = id;
                song.Title = songProperties.Title;
                song.Artist = songProperties.Artist;
                song.Album = songProperties.Album;
                song.AlbumCover = albumCover;
                song.SongFile = file;

                songs.Add(song);
                id++;
            }
        }

        private async void songGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            //Ignore clicks when we are cooling down
            if (!_playingMusic) return;

            CountDown.Pause();
            myMediaElement.Stop();

            var clickedSong = (Song) e.ClickedItem;
            //find the correct song
            var correctSong = songs.FirstOrDefault(p => p.isSelected == true);
            //Evaluate the user's selection
            Uri uri;
            int score;
            if (clickedSong.isSelected)
            {
                uri = new Uri("ms-appx:///Assets/correct.png");
                score = (int)myProgressBar.Value;
            }
            else
            {
                uri = new Uri("ms-appx:///Assets/incorrect.png");
                score = ( (int) myProgressBar.Value ) * -1;
            }
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var fileStream = await file.OpenAsync(FileAccessMode.Read);
            await clickedSong.AlbumCover.SetSourceAsync(fileStream);
            _totalScore += score;
            _round++;

            resultTextBlock.Text = string.Format("Score: {0} Total Score after {1} Rounds: {2}", score, _round, _totalScore);
            titletextBlock.Text = string.Format("Correct Song: {0}",correctSong.Title);
            artistTextBlock.Text = string.Format("Performed by: {0}", correctSong.Artist);
            albumTextBlock.Text = string.Format("On Album: {0}", correctSong.Album);

            clickedSong.isUsed = true;
            correctSong.isSelected = false;
            correctSong.isUsed = true;

            if (_round >= 5)
            {
                InstructionTextBlock.Text = string.Format("Game Over ... You scored: {0}", _totalScore);
                playAgainButton.Visibility = Visibility.Visible;
                
            }
            else
            {
                startCoolDown();
            }
        }

        private async void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            startupProgressRing.IsActive = true;
            allSongs = await setupMusicList();
            await prepareNewGame();
            startupProgressRing.IsActive = false;

            startCoolDown();
        }

        private async void playAgainButton_Click(object sender, RoutedEventArgs e)
        {
            await prepareNewGame();
            playAgainButton.Visibility = Visibility.Collapsed;
        }

        private async Task<ObservableCollection<StorageFile>> setupMusicList()
        {
            //Get Access to Music Library
            StorageFolder folder = KnownFolders.MusicLibrary;
            var allSongs = new ObservableCollection<StorageFile>();
            await retrieveFilesInFolders(allSongs, folder);
            return allSongs;
        }

        private async Task prepareNewGame()
        {
            songs.Clear();
            //Choose random songs from Library
            var randomSongs = await pickRandomSongs(allSongs);

            //Pluck off meta data from selected songs
            await populateSongList(randomSongs);

            startCoolDown();

            //State management
            InstructionTextBlock.Text = "Get Ready....";
            resultTextBlock.Text = "";
            artistTextBlock.Text = "";
            albumTextBlock.Text = "";
            titletextBlock.Text = "";
            _totalScore = 0;
            _round = 0;
        }

        private void startCoolDown()
        {
            _playingMusic = false;
            SolidColorBrush brush = new SolidColorBrush(Colors.Blue);
            myProgressBar.Foreground = brush;
            InstructionTextBlock.Text = String.Format("Get ready for round {0}", _round + 1);
            InstructionTextBlock.Foreground = brush;
            CountDown.Begin();
        }

        private void startCountDown()
        {
            _playingMusic = true;
            SolidColorBrush brush = new SolidColorBrush(Colors.Red);
            myProgressBar.Foreground = brush;
            InstructionTextBlock.Text = "GO!";
            InstructionTextBlock.Foreground = brush;
            CountDown.Begin();
        }

        private Song pickSong()
        {
            Random random = new Random();
            var unUsedSongs = songs.Where(p => p.isUsed == false);
            var randomNumber = random.Next(unUsedSongs.Count());
            var randomSong = unUsedSongs.ElementAt(randomNumber);
            randomSong.isSelected = true;
            return randomSong;
        }

        private async void CountDown_Completed(object sender, object e)
        {
            if (!_playingMusic)
            {
                //Start playing music
                var song = pickSong();

                myMediaElement.SetSource(await song.SongFile.OpenAsync(FileAccessMode.Read),song.SongFile.ContentType);

                //Start countdown   
                startCountDown();
            }
        }

        private async void purchaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AppLicenseInformation.ProductLicenses["MyInAppOfferToken"].IsActive)
            {
                try
                {
                    // The customer doesn't own this feature, so 
                    // show the purchase dialog.

                    PurchaseResults results = await CurrentAppSimulator.RequestProductPurchaseAsync("RemoveAdsOffer");

                    //Check the license state to determine if the in-app purchase was successful.

                    if (results.Status == ProductPurchaseStatus.Succeeded)
                    {
                        AdMediator_D1B3F5.Visibility = Visibility.Collapsed;
                        purchaseButton.Visibility = Visibility.Collapsed;
                    }
                }
                catch (Exception ex)
                {
                    // The in-app purchase was not completed because 
                    // an error occurred.
                    throw ex;
                }
            }
            else
            {
                // The customer already owns this feature.
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // Remove these lines of code before publishing!
            // The actual CurrentApp will create a WindowsStoreProxy.xml
            // in the package's \LocalState\Microsoft\Windows Store\ApiData
            // folder where it stores the actual purchases.
            // Here we're just giving it a fake version of that file
            // for testing.
            StorageFolder proxyDataFolder = await Package.Current.InstalledLocation.GetFolderAsync("Assets");
            StorageFile proxyFile = await proxyDataFolder.GetFileAsync("test.xml");
            await CurrentAppSimulator.ReloadSimulatorAsync(proxyFile);
            //++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            // You may want to put this at the App level
            AppLicenseInformation = CurrentAppSimulator.LicenseInformation;

            if (AppLicenseInformation.ProductLicenses["RemoveAdsOffer"].IsActive)
            {
                // Customer can access this feature.
                AdMediator_D1B3F5.Visibility = Visibility.Collapsed;
                purchaseButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Customer can NOT access this feature.
                AdMediator_D1B3F5.Visibility = Visibility.Visible;
                purchaseButton.Visibility = Visibility.Visible;
            }
        }
    }
}
