using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Image = SixLabors.ImageSharp.Image;


namespace WebPConverter
{
	public partial class MainWindow : Window
	{
		private List<string> selectedFiles = new();
		private string outputFolder;

		public MainWindow()
		{
			InitializeComponent();
			qualitySlider.ValueChanged += (s, e) => lblQuality.Text = ((int)qualitySlider.Value).ToString();

			// Set default output folder
			string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			outputFolder = System.IO.Path.Combine(documentsPath, "WebPOutput");
			Directory.CreateDirectory(outputFolder);
			txtOutputFolder.Text = outputFolder;
		}

		private void PickImages_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog
			{
				Multiselect = true,
				Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
			};

			if (dlg.ShowDialog() == true)
			{
				// Add new files without duplicates
				foreach (var file in dlg.FileNames)
				{
					if (!selectedFiles.Contains(file))
						selectedFiles.Add(file);
				}

				fileList.ItemsSource = null; // Refresh list
				fileList.ItemsSource = selectedFiles;

				if (selectedFiles.Count > 0)
					ShowPreview(selectedFiles.First());
			}
		}

		private void RemoveButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.DataContext is string file)
			{
				// Remove the file from the selected list
				selectedFiles.Remove(file);

				// Rebind the list to reflect changes
				fileList.ItemsSource = null;
				fileList.ItemsSource = selectedFiles;

				// If the list is empty, clear the preview
				if (selectedFiles.Count == 0)
				{
					previewImage.Source = null;
				}
				else if (fileList.SelectedItem == null)
				{
					// If no file is selected, set the preview to the first item
					ShowPreview(selectedFiles.First());
				}
			}
		}

		private void fileList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			if (fileList.SelectedItem is string selectedFile)
			{
				ShowPreview(selectedFile);  // Update preview based on selection
			}
		}


		private void Window_DragOver(object sender, DragEventArgs e)
		{
			// Make sure it's a file drop and contains valid image files
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				var files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files.Any(f => File.Exists(f) && IsImageFile(f)))
				{
					e.Effects = DragDropEffects.Copy;
				}
				else
				{
					e.Effects = DragDropEffects.None;
				}
			}
			e.Handled = true;
		}

		private void Window_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
				var imageFiles = droppedFiles.Where(f => File.Exists(f) && IsImageFile(f));

				foreach (var file in imageFiles)
				{
					if (!selectedFiles.Contains(file))
						selectedFiles.Add(file);
				}

				fileList.ItemsSource = null;
				fileList.ItemsSource = selectedFiles;

				if (selectedFiles.Count > 0)
					ShowPreview(selectedFiles.First());
			}
		}

		private void SelectOutputFolder_Click(object sender, RoutedEventArgs e)
		{
			using var dialog = new System.Windows.Forms.FolderBrowserDialog();
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				outputFolder = dialog.SelectedPath;
				txtOutputFolder.Text = outputFolder;
			}
		}

		private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
		{
			if (Directory.Exists(outputFolder))
			{
				Process.Start("explorer.exe", outputFolder);
			}
		}

		private void ShowPreview(string path)
		{
			if (File.Exists(path))
			{
				BitmapImage bitmap = new();
				bitmap.BeginInit();
				bitmap.UriSource = new Uri(path);
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				
				bitmap.EndInit();
				previewImage.Source = bitmap;
			}
		}

		private async void Convert_Click(object sender, RoutedEventArgs e)
		{
			if (selectedFiles.Count == 0)
			{
				MessageBox.Show("Please select images to convert.");
				return;
			}

			progressBar.Visibility = Visibility.Visible;
			progressBar.Value = 0;
			lblStatus.Text = "Converting...";

			await Task.Run(() =>
			{
				int count = 0;
				Stopwatch sw = Stopwatch.StartNew();

				foreach (var file in selectedFiles)
				{
					try
					{
						using Image image = Image.Load(file);
						image.Mutate(x => x.AutoOrient());

						string outputFile = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file) + ".webp");
						var encoder = new WebpEncoder { Quality = (int)Dispatcher.Invoke(() => qualitySlider.Value) };

						image.Save(outputFile, encoder);
					}
					catch (Exception ex)
					{
						Dispatcher.Invoke(() => MessageBox.Show($"Error converting {file}:\n{ex.Message}"));
					}

					count++;

					Dispatcher.Invoke(() =>
					{
						progressBar.Value = (double)count / selectedFiles.Count * 100;
						double avg = sw.Elapsed.TotalSeconds / count;
						double remaining = avg * (selectedFiles.Count - count);
						lblStatus.Text = $"Estimated time: {Math.Max(0, (int)remaining)}s";
					});
				}
			});

			Dispatcher.Invoke(() =>
			{
				progressBar.Visibility = Visibility.Collapsed;
				lblStatus.Text = "Conversion complete!";
				MessageBox.Show("All images converted to WebP.");
			});
		}

		private void ConvertToWebP(string inputPath)
		{
			try
			{
				using Image image = Image.Load(inputPath);
				image.Mutate(x => x.AutoOrient()); // Respect EXIF rotation

				string outputFile = System.IO.Path.Combine(outputFolder, System.IO.Path.GetFileNameWithoutExtension(inputPath) + ".webp");
				var encoder = new WebpEncoder { Quality = (int)qualitySlider.Value };
				image.Save(outputFile, encoder);
			}
			catch (Exception ex)
			{
				Dispatcher.Invoke(() =>
				{
					MessageBox.Show($"Error converting {inputPath}:\n{ex.Message}");
				});
			}
		}

		private void ClearAll_Click(object sender, RoutedEventArgs e)
		{
			
			selectedFiles.Clear();

			fileList.ItemsSource = null;

			previewImage.Source = null;

			lblStatus.Text = "No images selected.";
		}




		private bool IsImageFile(string path)
		{
			string ext = System.IO.Path.GetExtension(path).ToLower();
			return ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif";
		}
	}
}
