﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace Flickr_Downloader
{
	public struct ImageObject
	{
		public String name;
		public String url;

		public ImageObject(String name, String url)
		{
			this.name = name;
			this.url = url;
		}
	}

	internal class Program
	{
		static public void ProcessPage(Object workerNumber)
		{
			while (processQueue.Count > 0)
			{
				try
				{
					var imageID = "";
					lock (processQueue)
					{
						imageID = processQueue.Dequeue();
					}

					var webClient = new WebClient();
					var rImage = new Regex(String.Format("<img src=\"(?<url>https:\\/\\/\\w*\\.staticflickr\\.com\\/\\d*\\/{0}_\\w*\\.jpg)\">", imageID));
					var imagePage = webClient.DownloadString(String.Format(@"https://flickr.com/photos/{0}/{1}/sizes/k/", username, imageID));
					var imageUrl = rImage.Match(imagePage);
					if (imageUrl.Success)
					{
						lock (downloadQueue)
						{
							downloadQueue.Enqueue(new ImageObject(Path.GetFileName(imageUrl.Groups["url"].Value), imageUrl.Groups["url"].Value));
						}
					}
					else
					{
						Console.WriteLine("Failed to find image on '{0}'", imagePage);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}
			Console.WriteLine("Processer number {0} is done.", workerNumber);
			processCountdown.Signal();
		}

		static public void DownloadImage(Object workerNumber)
		{
			while (downloadQueue.Count > 0 || !processCountdown.IsSet)
			{
				if (downloadQueue.Count == 0)
				{
					Thread.Sleep(100);
					continue;
				}
				try
				{
					ImageObject image;
					lock (downloadQueue)
					{
						image = downloadQueue.Dequeue();
					}

					var webClient = new WebClient();
					webClient.DownloadFile(image.url, Path.Combine(destination, image.name));
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}
			Console.WriteLine("Downloader number {0} is done.", workerNumber);
			downloadCountdown.Signal();
		}

		private static Queue<ImageObject> downloadQueue;
		private static Queue<String> processQueue;
		private static String username;
		private static String destination;
		private static CountdownEvent downloadCountdown;
		private static CountdownEvent processCountdown;

		private static int threadCount = Environment.ProcessorCount > 1 ? Environment.ProcessorCount : 2;

		private static void Main(String[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("I need a url and a destination, wanker!");
				return;
			}

			var rUrl = new Regex(@"^https:\/\/www\.flickr\.com\/photos\/(?<name>\w*)\/sets\/\d*\/?$");

			var url = rUrl.Match(args[0]);
			if (!url.Success)
			{
				Console.WriteLine("Not a valid flickr url, knob!");
				return;
			}

			destination = args[1];

			if (!Directory.Exists(destination))
			{
				try
				{
					Directory.CreateDirectory(destination);
					Console.WriteLine("Seems like I had to create the destination folder for you, you lazy bum!");
				}
				catch (Exception ex)
				{
					Console.WriteLine("Couldn't create the destination folder because it's probably not a valid path, you twat!");
					Console.WriteLine(ex.Message);
					return;
				}
			}

			username = url.Groups["name"].Value;

			var rImageID = new Regex("id=\"photo_img_(?<id>\\d*)\"");
			var webClient = new WebClient();
			downloadCountdown = new CountdownEvent(threadCount);
			processCountdown = new CountdownEvent(threadCount);

			processQueue = new Queue<String>();
			downloadQueue = new Queue<ImageObject>();

			for (int i = 1; i < 50; i++)
			{
				try
				{
					var imageIDs = rImageID.Matches(webClient.DownloadString(String.Format("{0}/page{1}/", args[0], i)));
					if (imageIDs.Count > 0)
					{
						Console.WriteLine("Processing page number {0}", i);
						foreach (Match imageID in imageIDs)
						{
							processQueue.Enqueue(imageID.Groups["id"].Value);
						}
					}
					else
					{
						break;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}

			if (processQueue.Count > 0)
			{
				Console.WriteLine("Starting the threads");
				for (int i = 1; i < threadCount + 1; i++)
				{
					ThreadPool.QueueUserWorkItem(ProcessPage, i);
					ThreadPool.QueueUserWorkItem(DownloadImage, i);
				}
				Console.WriteLine("Waiting for threads");
				downloadCountdown.Wait();
				Console.WriteLine("All done!");
			}
			else
			{
				Console.WriteLine("Found nothing to process!\nPress any key to continue...");
				Console.ReadKey();
			}

			Console.WriteLine("Open destination folder? [Y/N]");
			if (Console.ReadKey().Key == ConsoleKey.Y)
			{
				Process.Start(destination);
			}
		}
	}
}