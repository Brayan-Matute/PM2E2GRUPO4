using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Newtonsoft.Json;
using Plugin.Media;
using Plugin.Media.Abstractions;
using SkiaSharp;
using System.Globalization;
using System.Timers;

namespace PM2E2GRUPO4
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class EditSitioPage : ContentPage
    {
        private Sitio _sitio;
        private string _base64Image;

        public EditSitioPage(Sitio sitio)
        {
            InitializeComponent();
            _sitio = sitio;
            BindingContext = _sitio;
            LoadSitioDetails();
        }

        private void LoadSitioDetails()
        {
            LongitudeEntry.Text = _sitio.longitud;
            LatitudeEntry.Text = _sitio.latitud;
            DescriptionEntry.Text = _sitio.descripcion;

            if (!string.IsNullOrEmpty(_sitio.fotografia))
            {
                imagen.Source = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(_sitio.fotografia)));
            }
        }

        private async void OnImageTapped(object sender, EventArgs e)
        {
            var cameraPermission = await Permissions.RequestAsync<Permissions.Camera>();

            if (cameraPermission != PermissionStatus.Granted)
            {
                await DisplayAlert("Permiso requerido", "Se necesitan permisos para acceder a la cámara y almacenamiento.", "OK");
                return;
            }

            await CrossMedia.Current.Initialize();

            if (!CrossMedia.Current.IsCameraAvailable || !CrossMedia.Current.IsTakePhotoSupported)
            {
                await DisplayAlert("No Camera", ":( No camera available.", "OK");
                return;
            }

            var file = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions
            {
                Directory = "Sample",
                Name = $"{DateTime.UtcNow}.jpg"
            });

            if (file == null)
                return;

            try
            {
                using (var inputStream = file.GetStream())
                using (var memoryStream = new MemoryStream())
                {
                    using (var skiaImage = SKBitmap.Decode(inputStream))
                    {
                        var resizedImage = ResizeImage(skiaImage, 800, 600);
                        using (var skiaImageResized = SKImage.FromBitmap(resizedImage))
                        using (var skiaImageData = skiaImageResized.Encode(SKEncodedImageFormat.Jpeg, 80))
                        {
                            skiaImageData.SaveTo(memoryStream);
                            _base64Image = Convert.ToBase64String(memoryStream.ToArray());
                        }
                    }
                }

                imagen.Source = ImageSource.FromStream(() => file.GetStream());
                _sitio.fotografia = _base64Image;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al convertir la imagen a Base64: {ex.Message}", "OK");
            }
        }

        private SKBitmap ResizeImage(SKBitmap originalImage, int maxWidth, int maxHeight)
        {
            int width, height;
            if (originalImage.Width > originalImage.Height)
            {
                width = maxWidth;
                height = originalImage.Height * maxHeight / originalImage.Width;
            }
            else
            {
                height = maxHeight;
                width = originalImage.Width * maxWidth / originalImage.Height;
            }

            return originalImage.Resize(new SKImageInfo(width, height), SKFilterQuality.Medium);
        }

        private async void OnSaveButtonClicked(object sender, EventArgs e)
        {
            _sitio.longitud = LongitudeEntry.Text;
            _sitio.latitud = LatitudeEntry.Text;
            _sitio.descripcion = DescriptionEntry.Text;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var json = JsonConvert.SerializeObject(_sitio);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(Config.Config.EndPointUpdate, content);

                    if (response.IsSuccessStatusCode)
                    {
                        await DisplayAlert("Éxito", "Sitio actualizado correctamente.", "OK");
                        await Navigation.PopAsync();
                    }
                    else
                    {
                        await DisplayAlert("Error", "Error al actualizar el sitio.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al actualizar el sitio: {ex.Message}", "OK");
            }
        }

        private async void OnCancelButtonClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Cancelar", "¿Estás seguro de que deseas cancelar? Los cambios no se guardarán.", "Sí", "No");
            if (confirm)
            {
                await Navigation.PopAsync();
            }
        }
    }
}
