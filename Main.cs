using System;
using System.IO;
using System.Reflection;

using System.Collections.Generic;

using Mono.Cecil;
using JsonFx.Json;

using UnityEngine;

using ScrollsModLoader.Interfaces;

namespace GallantDefenderBugFix
{
	public class Mod : BaseMod, ICommListener
	{
		private const string		MOD_NAME					= "Defender Fix";
		private const int			MOD_VERSION					= 1;

		private const string		DEBUG_MARKER				= "[MODSTACHE]";
		
		private const string		EMBEDDED_CARD_ART			= "GallantDefenderBugFix.assets.artwork.png";
		private const string		EMBEDDED_SPRITE_SHEET		= "GallantDefenderBugFix.assets.sprites.png";
		private const string		EMBEDDED_SPRITE_PREVIEW		= "GallantDefenderBugFix.assets.premade-sprite.png";

		private bool hasCardImage;
		private bool hasCardImageSmall;
		private bool hasAnimationBundle;

		private int gallantDefenderId;
		private string gallantDefenderCardImage;
		private string gallantDefenderAnimationPreview;
		private string gallantDefenderAnimationBundle;

		public Mod ()
		{
			try {
				App.Communicator.addListener(this);

			} catch {
				Log("ERROR: Unable to add self to CardTypeMessage Listener");
			}

			gallantDefenderId = -1;

			hasCardImage = false;
			hasCardImageSmall = false;
			hasAnimationBundle = false;
		}

		public void onConnect(OnConnectData data)
		{
			// intentionally blank
		}

		public void handleMessage(Message msg)
		{
			if (msg is CardTypesMessage) {
				JsonReader json = new JsonReader();
				Dictionary<string, object> jsonRoot = (Dictionary<string, object>) json.Read(msg.getRawText());
				Dictionary<string, object>[] cards = (Dictionary<string, object>[]) jsonRoot["cardTypes"];

				foreach(Dictionary<string, object> card in cards) {
					if (card["name"].Equals ("Gallant Defender")) {
						gallantDefenderId					= Convert.ToInt32(card["id"]);
						gallantDefenderCardImage			= card["cardImage"].ToString();
						gallantDefenderAnimationPreview		= card["animationPreviewImage"].ToString();
						gallantDefenderAnimationBundle		= "bundles/" + card["animationBundle"].ToString() + "/sprites.png";

						Log ("Gallant Defender found: " +
						     gallantDefenderId + ":" +
						     gallantDefenderCardImage + ":" +
						     gallantDefenderAnimationPreview + ":" +
						     gallantDefenderAnimationBundle);

						break;
					}
				}

				App.Communicator.removeListener(this);
			}
		}


		public static string GetName()
		{
			return MOD_NAME;
		}

		public static int GetVersion()
		{
			return MOD_VERSION;
		}

		public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version)
		{
			MethodDefinition[] definitions = new Mono.Cecil.MethodDefinition[] {
				scrollsTypes["CardTypeManager"].Methods.GetMethod ("feed")[1],
				scrollsTypes["CardImageCache"].Methods.GetMethod ("GetLoadedImage")[0],
				scrollsTypes["ResourceManager"].Methods.GetMethod ("tryGetTexture2D")[0],
				scrollsTypes["AssetLoader"].Methods.GetMethod ("LoadTexture2D")[0]
			};
			
			return definitions;
		}

		public override void BeforeInvoke (InvocationInfo info)
		{
			Log ("BeforeInvoke Called");
			Log ("    " + info.target.ToString() + ":" + info.targetMethod);

			if (info.target.ToString().Equals ("CardTypeManager") && info.targetMethod.Equals ("feed")) {
				if (info.arguments[0].ToString().Equals ("CardType[]")) {
					foreach (CardType t in ((CardType[]) info.arguments[0])) {
						if (t.name.Equals ("Gallant Defender")) {
							t.subTypesStr = "Reptile,Turtle";
							t.name = "Gallant Tortoise";

							t.description = "As long as you have no more units than your opponent, Gallant Tortoise has [Armor] 2.";

							Log (t.description);

							t.flavor = "\"Hold the walls! Protect y-- why are there turtles on the frontlines?!\"\n- Cay, Royal Envoy";

							Log (t.flavor);
						}
					}

				}

			} else if (info.target.ToString ().Equals ("CardImageCache") && info.targetMethod.Equals ("GetLoadedImage")) {
				if (hasCardImage == false && info.arguments[0].Equals (gallantDefenderCardImage)) {
					byte[] cardArt = GetEmbeddedAssetBytes(EMBEDDED_CARD_ART);
					
					if (cardArt != null) {
						CardImageCache cache = (CardImageCache) info.target;

						Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGB24, false);
						texture2D.LoadImage(cardArt);

						Dictionary<string, Texture2D> images = (Dictionary<string, Texture2D>)
							typeof(CardImageCache).GetField("_images", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(info.target);

						images.Add(gallantDefenderCardImage, texture2D);
						
						hasCardImage = true;
					}
				}

			} else if (info.target.ToString ().Equals ("ResourceManager") && info.targetMethod.Equals ("tryGetTexture2D")) {
				if (hasAnimationBundle == false && info.arguments[0].Equals (gallantDefenderAnimationBundle)) {
					byte[] spritesheet = GetEmbeddedAssetBytes(EMBEDDED_SPRITE_SHEET);
					
					if (spritesheet != null) {
						ResourceManager cache = (ResourceManager) info.target;
						
						Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGB24, false);
						texture2D.LoadImage(spritesheet);
						
						Dictionary<string, Texture2D> images = (Dictionary<string, Texture2D>)
							typeof(ResourceManager).GetField("_textures2D", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(info.target);
						
						images.Add(gallantDefenderAnimationBundle, texture2D);
						
						hasAnimationBundle = true;
					}
				}

			}

			return;
		}
		
		public override void AfterInvoke (ScrollsModLoader.Interfaces.InvocationInfo info, ref object returnValue)
		{
			Log ("AfterInvoke Called");
			Log ("    " + info.target.ToString() + ":" + info.targetMethod);

			if (info.target.ToString ().Equals ("App (AssetLoader)") && info.targetMethod.Equals ("LoadTexture2D")) {
				if (info.arguments[0].Equals (gallantDefenderCardImage)) {
					// inefficient, I know; but I'm too lazy to spend the minute needed to create a member variable to store the created texture in... yeah.
					byte[] preview = GetEmbeddedAssetBytes(EMBEDDED_CARD_ART);
					
					if (preview != null) {
						Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGB24, false);
						texture2D.LoadImage(preview);

						returnValue = texture2D;
					}
				}
			}

			return;
		}

		private byte[] GetEmbeddedAssetBytes(String asset) {
			Log ("Retrieving embedded asset: " + asset);

			Assembly assembly = Assembly.GetExecutingAssembly();
			
			Stream resource = assembly.GetManifestResourceStream(asset);
			if (resource == null) {
				Log("ERROR: Unable to retrieve " + asset);
				
			} else {
				byte[] data = new byte[resource.Length];
				
				resource.Read (data, 0, data.Length);
				resource.Close();
				
				return data;
			}

			return null;
		}

		private static void Log(String message)
		{
			Console.WriteLine (DEBUG_MARKER + " " + message);
		}

	}
}
