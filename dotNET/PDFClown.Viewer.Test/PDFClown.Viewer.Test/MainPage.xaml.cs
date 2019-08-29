using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.Forms;

namespace PdfClown.Viewer.Test
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnOpenFileClicked(object sender, EventArgs e)
        {
            var service = DependencyService.Get<IOpenFileService>();
            try
            {
                var fileInfo = await service.OpenFileDialog();
                if (fileInfo.Stream != null)
                {
                    label.Text = Path.GetFileName(fileInfo.FileName);
                    viewer.Load(fileInfo.Stream);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error: " + ex.GetType().Name, ex.Message, "Close");
            }
        }
    }
}
