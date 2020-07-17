using Newtonsoft.Json;
using PaisleyPark.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Windows;
using System.Windows.Input;

namespace PaisleyPark.ViewModels
{
	public class ImportViewModel : BindableBase
	{
		public string ImportText { get; set; }
		public bool? DialogResult { get; set; } = false;
		public ICommand ImportCommand { get; private set; }

		private readonly static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

		public Preset ImportedPreset;

		public ImportViewModel()
		{
			ImportCommand = new DelegateCommand(OnImport);
		}

		private void OnImport()
		{
			logger.Info("Importing JSON");

			try
			{
				// Deserialize JSON into Preset object.
				ImportedPreset = JsonConvert.DeserializeObject<Preset>(ImportText);

				// Checking validity purely by the name being specified. Could use a more robust check but this is good enough.
				if (ImportedPreset.Name == null || ImportedPreset.Name.Trim() == string.Empty)
				{
					MessageBox.Show("这不是一个有效的预设，导入失败.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
				MessageBox.Show("导入预设 " + ImportedPreset.Name + "!", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				DialogResult = true;
			}
			// Likely not a JSON string.
			catch (Exception ex)
			{
				logger.Error(ex, "Error trying to import JSON\n{0}", ImportText);
				MessageBox.Show("无效输入，这不是有效的JSON。无法导入预设.", "Paisley Park", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}
}
