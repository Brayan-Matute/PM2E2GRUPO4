using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Newtonsoft.Json;
using Plugin.Maui.Audio;
using System.IO;

namespace PM2E2GRUPO4;

public partial class AudioListPage : ContentPage


{

    ObservableCollection<ApiAudioFile> _audioFiles;
    public AudioListPage()
	{
		InitializeComponent();

        _audioFiles = new ObservableCollection<ApiAudioFile>();
        AudioCollectionView.ItemsSource = _audioFiles;

        LoadAudioFiles();
    }


    private async void LoadAudioFiles()
    {
        _audioFiles.Clear();
        await GetAudioFilesFromApi();
        AudioCollectionView.IsVisible = _audioFiles.Any();
    }

    private async void OnRefreshAudioListClicked(object sender, EventArgs e)
    {
        await GetAudioFilesFromApi();
    }

    private async Task GetAudioFilesFromApi()
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(Config.Config.EndPointList);

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    var audioFilesFromApi = JsonConvert.DeserializeObject<List<ApiAudioFile>>(content);

                    foreach (var audioFile in audioFilesFromApi)
                    {
                        _audioFiles.Add(audioFile);
                    }
                }
                else
                {
                    await DisplayAlert("Error", $"Error al obtener la lista de audios. StatusCode: {response.StatusCode}", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Excepción: {ex.Message}", "OK");
        }
    }

    private void OnPlaySwipeItemInvoked(object sender, EventArgs e)
    {
        var swipeItem = sender as SwipeItem;
        var audioFile = swipeItem.BindingContext as ApiAudioFile;
        if (audioFile != null)
        {
            var player = AudioManager.Current.CreatePlayer(File.OpenRead(audioFile.FilePath));
            player.Play();

            DisplayAlert("Reproduciendo", $"Reproduciendo {audioFile.Descripcion}", "OK");
        }
    }

    private async void OnDeleteSwipeItemInvoked(object sender, EventArgs e)
    {
        var swipeItem = sender as SwipeItem;
        var audioFile = swipeItem.CommandParameter as ApiAudioFile;
        if (audioFile != null)
        {
            bool confirmed = await DisplayAlert("Eliminar", $"¿Estás seguro de eliminar {audioFile.Descripcion}?", "Sí", "No");
            if (confirmed)
            {
                _audioFiles.Remove(audioFile);
            }
        }
    }
}

public class ApiAudioFile
{
    public int Id { get; set; }
    public string Aduios { get; set; }
    public string Fecha { get; set; }
    public string Descripcion { get; set; }

    public string FilePath => Aduios;
}