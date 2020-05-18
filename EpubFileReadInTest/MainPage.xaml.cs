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
using EpubFileReadInTest;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.IO.Compression;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;
using Microsoft.Graphics.Canvas.Effects;
using Windows.UI;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace EpubFileReadInTest
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            DeleteButton.Visibility = Visibility.Collapsed;
            
        }

        public string rootFolderString = null;

        private async void CoverButton_Click(object sender, RoutedEventArgs e)
        {
            ProgressRing.Visibility = Visibility.Visible;
            ProgressRing.IsActive = true;

            Bookinfo bookinfo = new Bookinfo();

            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".epub");
            var file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                try
                {
                    rootFolderString = await EpubReadIn.EpubCopyAsync(file);
                    if (rootFolderString != null)
                    {
                        bookinfo = await EpubAnalyze.GetBookinfo(rootFolderString);
                        if (bookinfo != null)
                        {
                            this.ExceptionTextBlock.Text = rootFolderString;
                            BitmapImage bi = new BitmapImage();
                            IRandomAccessStream ir = await bookinfo.Cover.OpenAsync(FileAccessMode.Read);
                            await bi.SetSourceAsync(ir);
                            BackgroundImage.Source = bi;
                            Cover.Source = bi;
                            TitleTextBlock.Text = bookinfo.Title;
                            AuthorTextBlock.Text = bookinfo.Creator;
                            PublisherTextBlock.Text = bookinfo.Publisher;
                            LanguageTextBlock.Text = bookinfo.Language;
                            DateTextBlock.Text = bookinfo.Date;
                            InitializeFrostedGlass(GlassHost);
                            DeleteButton.Visibility = Visibility.Visible;

                        }
                        else
                        {
                            ExceptionTextBlock.Text = "出现问题.";
                        }

                    }
                    else
                    {
                        ExceptionTextBlock.Text = "读取文件时出现问题.";
                    }
                }
                catch (Exception ex)
                {
                    ExceptionTextBlock.Text = ex.Message;
                    await EpubDelete.DeleteBothAsync(rootFolderString);

                }

            }
            else
            {
                ExceptionTextBlock.Text ="打开文件操作被取消.";
            }

            ProgressRing.Visibility = Visibility.Collapsed;
            ProgressRing.IsActive = false;


        }

        private void DirButton_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (rootFolderString != null)
            {
                await EpubDelete.DeleteDirAsync(rootFolderString);
            }
        }

        private bool HandleException(Exception ex)
        {
            throw new NotImplementedException();
        }

        private void InitializeFrostedGlass(UIElement glassHost)
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(glassHost);
            Compositor compositor = hostVisual.Compositor;
            var glassEffect = new GaussianBlurEffect
            {
                BlurAmount = 40.0f,
                BorderMode = EffectBorderMode.Hard,
                Source = new ArithmeticCompositeEffect
                {
                    MultiplyAmount = 0,
                    Source1Amount = 0.5f,
                    Source2Amount = 0.5f,
                    Source1 = new CompositionEffectSourceParameter("backdropBrush"),
                    Source2 = new ColorSourceEffect { Color = Color.FromArgb(255, 255, 255, 255) }
                }
            };
            var effectFactory = compositor.CreateEffectFactory(glassEffect);
            var backdropBrush = compositor.CreateBackdropBrush();
            var effectBrush = effectFactory.CreateBrush();
            effectBrush.SetSourceParameter("backdropBrush", backdropBrush);
            var glassVisual = compositor.CreateSpriteVisual();
            glassVisual.Brush = effectBrush;
            ElementCompositionPreview.SetElementChildVisual(glassHost, glassVisual);
            var bindSizeAnimation = compositor.CreateExpressionAnimation("hostVisual.Size");
            bindSizeAnimation.SetReferenceParameter("hostVisual", hostVisual);
            glassVisual.StartAnimation("Size", bindSizeAnimation);
        }

    }
}
