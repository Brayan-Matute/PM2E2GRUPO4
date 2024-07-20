using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Plugin.Maui.Audio;

namespace PM2E2GRUPO4
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ListaSitiosPage : ContentPage
    {
        private Sitio _selectedSitio;
        private IAudioPlayer _audioPlayer;

        public ListaSitiosPage()
        {
            InitializeComponent();
            LoadSitiosAsync();
        }

        private async void LoadSitiosAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var response = await client.GetStringAsync(Config.Config.EndPointList);
                    var sitios = JsonSerializer.Deserialize<List<Sitio>>(response, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    SitiosCollectionView.ItemsSource = sitios;
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Error al cargar sitios: {ex.Message}", "OK");
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedSitio != null)
            {
                _selectedSitio.IsSelected = false;
            }

            _selectedSitio = e.CurrentSelection.FirstOrDefault() as Sitio;

            if (_selectedSitio != null)
            {
                _selectedSitio.IsSelected = true;
            }

            // Update the UI to reflect the selection
            SitiosCollectionView.ItemsSource = SitiosCollectionView.ItemsSource; // Refresh CollectionView
        }

        private async void OnPlayAudioClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            var sitio = button?.CommandParameter as Sitio;

            if (sitio != null)
            {
                try
                {
                    // Verifica si el archivo de audio es válido
                    if (string.IsNullOrEmpty(sitio.audiofile))
                    {
                        await DisplayAlert("Error", "El archivo de audio está vacío o no está disponible.", "OK");
                        return;
                    }

                    // Decodifica el audio desde Base64
                    byte[] audioData;
                    try
                    {
                        audioData = Convert.FromBase64String(sitio.audiofile);
                    }
                    catch (FormatException)
                    {
                        await DisplayAlert("Error", "El archivo de audio no está en un formato Base64 válido.", "OK");
                        return;
                    }

                    // Reproduce el audio
                    using var audioStream = new MemoryStream(audioData);
                    _audioPlayer = AudioManager.Current.CreatePlayer(audioStream);
                    _audioPlayer.Play();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Error al reproducir audio: {ex.Message}", "OK");
                }
            }
        }

        private void OnStopAudioClicked(object sender, EventArgs e)
        {
            if (_audioPlayer != null && _audioPlayer.IsPlaying)
            {
                _audioPlayer.Stop();
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }
        }

        private void OnItemTapped(object sender, EventArgs e)
        {
            var frame = sender as Frame;
            var sitio = frame.BindingContext as Sitio;
            _selectedSitio = sitio;
            SitiosCollectionView.SelectedItem = _selectedSitio;
        }

        private async void OnUpdateClicked(object sender, EventArgs e)
        {
            var swipeItem = sender as SwipeItem;
            var sitio = swipeItem?.CommandParameter as Sitio;

            if (sitio != null)
            {
                // Aquí puedes implementar la lógica para actualizar el sitio.
                // Por ejemplo, abrir una nueva página para editar los detalles del sitio.
                await DisplayAlert("Actualizar", $"Actualizar sitio: {sitio.descripcion}", "OK");
            }
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            var swipeItem = sender as SwipeItem;
            var sitio = swipeItem?.CommandParameter as Sitio;

            if (sitio != null)
            {
                bool confirm = await DisplayAlert("Eliminar", $"¿Estás seguro de que deseas eliminar el sitio: {sitio.descripcion}?", "Sí", "No");
                if (confirm)
                {
                    using (HttpClient client = new HttpClient())
                    {
                        try
                        {
                            var content = new StringContent(JsonSerializer.Serialize(new { id = sitio.Id }), System.Text.Encoding.UTF8, "application/json");
                            var response = await client.PostAsync(Config.Config.EndPointDelete, content);

                            if (response.IsSuccessStatusCode)
                            {
                                await DisplayAlert("Éxito", "Sitio eliminado correctamente.", "OK");
                                LoadSitiosAsync(); 
                            }
                            else
                            {
                                await DisplayAlert("Error", "Error al eliminar el sitio.", "OK");
                            }
                        }
                        catch (Exception ex)
                        {
                            await DisplayAlert("Error", $"Error al eliminar el sitio: {ex.Message}", "OK");
                        }
                    }
                }
            }
        }
        private async void OnMapaClicked(object sender, EventArgs e)
        {

            var swipeItem = sender as SwipeItem;
            var sitio = swipeItem?.CommandParameter as Sitio;

            if (sitio != null)
            {
                
                await Navigation.PushAsync(new PageMap(
                    Convert.ToDouble(sitio.latitud),
                    Convert.ToDouble(sitio.longitud),
                    sitio.descripcion,
                    sitio.fotografia));
            }
        }
    }

    public class Sitio
    {
        public string Id { get; set; }
        public string audiofile { get; set; }
        public string descripcion { get; set; }
        public string longitud { get; set; }
        public string latitud { get; set; }
        public string fotografia { get; set; }
        public bool IsSelected { get; set; }
    

    public ImageSource ImageSource
    {
        get
        {
            try
            {
                if (string.IsNullOrEmpty(fotografia))
                {
                    return null;
                }
                byte[] imageBytes = Convert.FromBase64String(fotografia);
                return ImageSource.FromStream(() => new MemoryStream(imageBytes));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al convertir Base64 string a Imagen: {ex.Message}");
                return null;
            }
        }
    }
}
}

