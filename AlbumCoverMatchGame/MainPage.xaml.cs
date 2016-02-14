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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AlbumCoverMatchGame
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<Song> songs;
        public MainPage()
        {
            this.InitializeComponent();

            songs = new ObservableCollection<Song>();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            //Get Access to Music Library
            StorageFolder folder = KnownFolders.MusicLibrary;
            var allSongs = new ObservableCollection<StorageFile>();
            await retrieveFilesInFolders(allSongs, folder);

            //Choose random songs from Library
            var randomSongs = await pickRandomSongs(allSongs);

            //Pluck off meta data from selected songs
            await populateSongList(randomSongs); 

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
    }
}
