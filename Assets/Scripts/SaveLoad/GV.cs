﻿using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;


/// <summary>
/// ゲーム共通用のクラス
/// ロード時に値が入る予定
/// </summary>
/// <remarks>
/// クラスが参照されたタイミングでインスタンスが生成されます
/// Singlton を採用
/// </remarks>
public sealed class GV {
	#region Classes

	////////////////////////////////////////////
	int saveLoadSlot = 1; // 初期セーブスロット
	/// <summary>Title.csのsaveおよびloadでのみ値セットするものとする</summary>
	public int slot { get { return saveLoadSlot; } set { saveLoadSlot = value; } }
	///////////////////////////////////////////

	private const int PLAYERS = 6;

	private static GV mInstance = new GV( );
	/// <summary>Singlton</summary>
	public static GV Instance { get { return mInstance; } }

	/*===============================================================*/
	/// <summary>コンストラクター</summary>
	private GV( ) {
		Debug.Log( "Create GV Instance." );

		// インスタンスの作成
		// 毎回 new すると前のセーブデータが消えるので一回だけ new する
		gameData = new GameData( );

		// player
		gameData.Players = new List<SingltonPlayerManager.PlayerParameters>( );
		// equip
		gameData.Equipments = new List<EquipmentParam>( );
		// skill
		gameData.PlayersSkills = new List<SkillParam>( );
		// item
		gameData.Items = new ItemParam( );
        gameData.Items.itemList = new Dictionary<string, int>();
		// event
		gameData.Flag = new FlagManage( );
		gameData.Flag.EventFlag = new Dictionary<string, bool>( );
		// spawn
		gameData.spawnPlayer = new SpawnScene( );

		for( int i = 0; i < PLAYERS /* !変更しない! */; i++ ) {
			// player
			gameData.Players.Add( new SingltonPlayerManager.PlayerParameters( ) );
			// skill
			gameData.Players[ i ].SkillList = new List<string>( );
			gameData.PlayersSkills.Add( new SkillParam( ) );
			gameData.PlayersSkills[ i ].Name = new List<string>( );
			// equip
			gameData.Equipments.Add( new EquipmentParam( ) );

		}

		// fixed time 初期化
		initTime( );

		// データのロードを行う ( 前回のデータ保持 )
		GameDataLoad( slot );

		// 一秒ごとに, タイムカウントアップします
		Observable
			.Interval( TimeSpan.FromSeconds( 1 ) )
			.Subscribe( x => {
				TimeCountUp( );
			}
		);



	}
	/*===============================================================*/

	/// <summary>
	/// Save するゲームデータ
	/// </summary>
	[Serializable]
	public class GameData {
		#region Other
		/// <summary>プレイ時間[秒]計測用</summary>
		public int timeSecond;
		/// <summary>save,loadされた時に格納されるプレイタイム</summary>
		/// <remarks>
		/// TimeSpan t = new TimeSpan( 0, 0, gameData.fixedTime );
		/// 上の様に変換しプレイ時間を取得します
		/// </remarks>
		public int[ ] fixedTime;
		/// <summary>イベントフラグ管理</summary>
		public FlagManage Flag;
		/// <summary>出現場所(プレイヤー)</summary>
		public SpawnScene spawnPlayer;
		#endregion

		#region Player
		public List<SingltonPlayerManager.PlayerParameters> Players;
		public List<SkillParam> PlayersSkills;
		//public int PartyCount;
		#endregion

		#region Items
		// 所持している装備のIDの配列
		//public List<int> Equipments;
		public List<EquipmentParam> Equipments;
		/// <summary>現在所持しているアイテム一覧</summary>
		public ItemParam Items;
        #endregion


    }

	// ゲームデータを読み込むときは先にセーブデータのロードまたはnewGame で関数を初期化して使用
	private GameData gameData;
	public GameData GData { get { return gameData; } }

	[Serializable]
	public class SystemData {
		public bool[ ] usedSave;
		public SystemData( ) {
			usedSave = new bool[ SaveData.SaveSlotCount ];
		}
	}

	private SystemData systemData;
	public SystemData SData {
		get {
			if ( systemData == null ) {
				gameAwake( );
			}
			return systemData;
		}
	}

	#endregion

	#region PropertiSTATUS
	#endregion

	/// <summary>
	/// ゲーム起動時に呼び出し
	/// </summary>
	public void gameAwake( ) {
		// SystemDataの読み込み
		SaveData.setSlot( 0 );
		SaveData.load( );
		systemData = SaveData.getClass<SystemData>( "SystemData", null );
		if ( systemData == null ) {
			systemData = new SystemData( );
		}
	}

	/// <summary>
	/// ゲームを新しく始めるときに呼び出される関数
	/// </summary>
	/// <param name="audioManager">オーディオマネージャーの引数</param>
	/// <remarks>Title.csのnewGameButton.OnClickAsObservable()から呼ばれる</remarks>
	public void newGame( GameObject audioManager ) {

		Debug.Log( "<color='red'>newGame Function Called.</color>" );

		// save data file delete, save prefab で任意のセーブスロットを選択すると
		// 選択されたファイルが一旦, 削除される, その後, 初期化されたデータで保存される
		//SaveData.remove( slot );

		// save data を扱う各, singlton class の初期化処理
		// player
		SingltonPlayerManager.Instance.SaveDataPlayerState.Clear( );
		SingltonPlayerManager.Instance.NewGame( );
		// equip
		SingltonEquipmentManager.Instance.SaveDataPlayerEquipmentParam.Clear( );
		SingltonEquipmentManager.Instance.NewGame( );
		// item
		SingltonItemManager.Instance.SDItem.itemList.Clear( );
		SingltonItemManager.Instance.NewGame( );
		// skill
		SingltonSkillManager.Instance.SDSkill.Clear( );
		SingltonSkillManager.Instance.NewGame( );
		// Event
		SingltonFlagManager.Instance.NewGame( );
		// Spawn
		SingltonSceneManage.Instance.NewGame( );
		// time
		gameData.timeSecond = 0;

		GameDataSave( slot );
        SceneController.sceneTransition(SceneName.SceneNames.Prologue, 2.0f, SceneController.FadeType.Fade);
		UnityEngine.Object.DontDestroyOnLoad( audioManager );
	}

	/*===============================================================*/
	/// <summary>TitleでのLoad時にパラメーターを読み込みます</summary>
	/// <param name="slotIndex">ロードするスロット番号</param>
	public void SlotChangeParamUpdate( int slotIndex ) {
		Debug.Log( "<color='red'>各種パラメーターが更新されました。</color>" );
		SingltonPlayerManager.Instance.LoadPlayer( slotIndex ); // slot を変えたときに値を更新する
		SingltonEquipmentManager.Instance.LoadEquipment( slotIndex );
		SingltonSkillManager.Instance.LoadSkill( slotIndex );
		SingltonItemManager.Instance.LoadItem( slotIndex );
		SingltonFlagManager.Instance.LoadFlag( slotIndex );
		SingltonSceneManage.Instance.LoadSpawn( slotIndex );
		gameData.fixedTime[ slotIndex ] = gameData.timeSecond;

		// Scene 遷移
		SingltonSceneManage.Instance.SceneTranslation( SingltonSceneManage.Instance.SDSpawn.currentSpawnPlayer );


	}
	/*===============================================================*/

	/*===============================================================*/
	/// <summary>時間をカウントアップします</summary>
	private void TimeCountUp( ) {
		//myTimeSecond = SaveData.getInt( slot + SaveDataKey.KEY_CURRENT_TIME, -1 );
		gameData.timeSecond += 1; // 一秒づつカウント
		//TimeSpan newTime = new TimeSpan( 0, 0, gameData.timeSecond[ slot ] );
		//Debug.Log( "<color='red'>newTime : " + newTime + ", myTimeSecond : " + gameData.timeSecond[ slot ] + " </color>" );
		if( gameData.timeSecond == -1 ) Debug.LogError( "TimeCountUp Function on the Error." );


	}
	/*===============================================================*/

	/*===============================================================*/
	/// <summary>固定タイムの初期化を行います</summary>
	private void initTime( ) {
		gameData.fixedTime = new int[ 4 ];
		// save data 1 ～ 3 roop
		for( int i = 0; i < 3; i++ ) {
			SaveData.setSlot( i + 1 );
			SaveData.load( );
			gameData.fixedTime[ i + 1 ] = SaveData.getInt( SaveDataKey.KEY_CURRENT_TIME, 0 );

		}


	}
	/*===============================================================*/

	/*===============================================================*/
	/// <summary>セーブデータに保存されているKEYのデバッグ出力を行います</summary>
	public void DebugKeyPrint( ) {
		foreach ( string items in SaveData.getKeys( ) ) {
			Debug.Log( "------------------\nセーブデータキー :\n" + items + "\n------------------" );

		}


	}
	/*===============================================================*/

	/*===============================================================*/
	/// <summary>セーブデータキー定義</summary>
	public class SaveDataKey {
		public const string PRAYER_PARAM = "PLAYER_PARAM";
		public const string EQUIPMENT_PARAM = "EQUIPMENT_PARAM";
		public const string ITEM_PARAM = "ITEM_PARAM";
		public const string ITEM_PARAM2 = "ITEM_PARAM2";
		public const string SKILL_PARAM = "SKILL_PARAM";
		public const string KEY_CURRENT_TIME = "KEY_CURRENT_TIME";
		public const string FLAG_PARAM = "FLAG_PARAM";
		public const string SPAWN_PARAM = "SPAWN_PARAM";


	}
	/*===============================================================*/

	/*===============================================================*/
	/// <summary>セットした変動値をセーブデータから読み込みます</summary>
	/// <remarks>
	/// コンティニューなどに使います
	/// (注意) gamemaneger class などで最初に save data を扱う,player singlton class や
	/// equip singlton class などのインスタンスを作成しておかないと各,singlton class
	/// のloadで前の値が読み込まれる可能性があるので gamemanager class に一括で作成するようにします。
	/// </remarks>
	/// <param name="loadSlot">ロードするセーブデータスロットを指定します</param>
	public void GameDataLoad( int loadSlot ) {

		SaveData.setSlot( loadSlot );
		SaveData.load( );

		/*===============================================================*/
		// プレイヤーステータス情報読込
		List<SingltonPlayerManager.PlayerParameters> myPlayerState = SaveData.getList<SingltonPlayerManager.PlayerParameters>( GV.SaveDataKey.PRAYER_PARAM );

		for ( int i = 0; i < gameData.Players.Count; i++ ) {
			if ( myPlayerState != null ) {
				gameData.Players[ i ].Lv = myPlayerState[ i ].Lv;
				gameData.Players[ i ].currentFeel = myPlayerState[ i ].currentFeel;
				gameData.Players[ i ].SkillList = myPlayerState[ i ].SkillList;
                gameData.Players[i].STATUS = myPlayerState[i].STATUS;

			}

		}
		/*===============================================================*/

		/*===============================================================*/
		// 装備情報読込
		List<SingltonEquipmentManager.PlayerEquipmentParam> myEquip = SaveData.getList<SingltonEquipmentManager.PlayerEquipmentParam>( GV.SaveDataKey.EQUIPMENT_PARAM );

		for( int i = 0; i < gameData.Equipments.Count; i++ ) {
			if ( myEquip != null ) {
				gameData.Equipments[ i ].ID = myEquip[ i ].ID;
				gameData.Equipments[ i ].Arms = myEquip[ i ].Arms;
				gameData.Equipments[ i ].Armor = myEquip[i].Armor;
				gameData.Equipments[ i ].Head = myEquip[ i ].Head;
				gameData.Equipments[ i ].Shoes = myEquip[ i ].Shoes;
				gameData.Equipments[ i ].Accessory1 = myEquip[ i ].Accessory1;
				gameData.Equipments[ i ].Accessory2 = myEquip[ i ].Accessory2;

			}

		}
		/*===============================================================*/

		/*===============================================================*/
		// アイテム情報読込
		// Dictionary Type
		Dictionary<string, int> myItem = SaveData.getDictionary<string, int>( GV.SaveDataKey.ITEM_PARAM );
		// Class Type
		SingltonItemManager.ItemParam myItem2 = SaveData.getClass<SingltonItemManager.ItemParam>( GV.SaveDataKey.ITEM_PARAM2 );

		if( myItem != null ) {
			gameData.Items.itemList = myItem;

		}
		if( myItem2 != null ) {
			gameData.Items.possessionGolds = myItem2.possessionGolds;

		}

		/*===============================================================*/

		/*===============================================================*/
		// スキル情報読込
		List<SingltonSkillManager.SkillParam> mySkill = SaveData.getList<SingltonSkillManager.SkillParam>( GV.SaveDataKey.SKILL_PARAM );

		for( int i = 0; i < gameData.PlayersSkills.Count; i++ ) {
			if ( mySkill != null ) {
				gameData.PlayersSkills[ i ].Name = mySkill[ i ].Name;

			}

		}
		/*===============================================================*/

		/*===============================================================*/
		// イベント情報読込
		Dictionary<string, bool> myFlag = SaveData.getDictionary<string, bool>( GV.SaveDataKey.FLAG_PARAM );

		if ( myFlag != null ) {
			gameData.Flag.EventFlag = myFlag;

		}

		/*===============================================================*/

		/*===============================================================*/
		// 出現場所情報読込
		SingltonSceneManage.SpawnScene mySpawn = SaveData.getClass<SingltonSceneManage.SpawnScene>( GV.SaveDataKey.SPAWN_PARAM );

		if( mySpawn != null ) {
			if( mySpawn.currentSpawnPlayer != "" ) gameData.spawnPlayer.currentSpawnPlayer = mySpawn.currentSpawnPlayer;
			else if( mySpawn.currentSpawnPlayer == "" ) gameData.spawnPlayer.currentSpawnPlayer =  "azito";

		}

		/*===============================================================*/

		// ロードスロット + キーでプレイ時間を読み込む
		if( loadSlot > 0 ) {
			gameData.timeSecond = SaveData.getInt( SaveDataKey.KEY_CURRENT_TIME, 0 );
			gameData.fixedTime[ slot ] = gameData.timeSecond;

		}

		//TimeSpan t = new TimeSpan( 0, 0, gameData.fixedTime );
		//Debug.Log( "<color='red'>" + loadSlot + ", " + saveLoadSlot + ", playTime : " + t + "</color>" );


	}
	/*===============================================================*/

	/*===============================================================*/
	/// <summary>セーブを行います</summary>
	/// <param name="saveSlot">セーブするセーブデータスロットを指定します</param>
	public void GameDataSave( int saveSlot ) {

		SData.usedSave[ saveSlot - 1 ] = true;

		SaveData.setSlot( 0 );
		SaveData.setClass( "SystemData", SData );
		SaveData.save( );

		SaveData.setSlot( saveSlot );

		/*===============================================================*/
		// セーブデータへの値セット部 START

		SaveData.setList( SaveDataKey.PRAYER_PARAM, SingltonPlayerManager.Instance.SaveDataPlayerState ); // プレイヤーパラメーター
		SaveData.setList( SaveDataKey.EQUIPMENT_PARAM, SingltonEquipmentManager.Instance.SaveDataPlayerEquipmentParam ); // プレイヤー装備パラメーター
		SaveData.setList( SaveDataKey.SKILL_PARAM, SingltonSkillManager.Instance.SDSkill ); // プレイヤースキルパラメーター
		SaveData.setDictionary( SaveDataKey.ITEM_PARAM, SingltonItemManager.Instance.SDItem.itemList ); // アイテムパラメーター
		SaveData.setClass( SaveDataKey.ITEM_PARAM2, SingltonItemManager.Instance.SDItem ); // アイテムパラメーター 2 Dictionary 以外のデータ
		SaveData.setDictionary( SaveDataKey.FLAG_PARAM, SingltonFlagManager.Instance.SDFlg.EventFlag ); // イベントパラメーター
		SaveData.setClass( SaveDataKey.SPAWN_PARAM, SingltonSceneManage.Instance.SDSpawn ); // 出現場所パラメーター
		

		// セーブデータへの値セット部 END
		/*===============================================================*/

		SaveData.setInt( SaveDataKey.KEY_CURRENT_TIME, gameData.timeSecond );
		gameData.fixedTime[ saveSlot ] = gameData.timeSecond;

		SaveData.save( );

		//TimeSpan t = new TimeSpan( 0, 0, gameData.timeSecond[ saveSlot ] );
		//Debug.Log( "<color='red'>" + saveSlot + ", " + saveLoadSlot + ", playTime : " + t + "</color>" );


	}
	/*===============================================================*/

	/*===============================================================*/
	// パラメーター部 START

	/// <summary>
	/// プレイヤーの情報
	/// 装備後ステータス(Save)
	/// 変動する情報を保存用
	/// </summary>
	[Serializable]
	public class PlayerParam {
		/// <summary>レベル(Save)</summary>
		public int Lv;
		/// <summary>現在の感情値:CurrentFeelingValue(Save)</summary>
		public int CFV;
		/// <summary>現在覚えているスキルIDリスト(Save)</summary>
		public List<string> SkillList;
		/// <summary>ヒットポイント</summary>
		public int HP;
		/// <summary>マジックポイント</summary>
		public int MP;
		/// <summary>攻撃力</summary>
		public int Atk;
		/// <summary>防御力</summary>
		public int Def;
		/// <summary>魔法攻撃力</summary>
		public int Matk;
		/// <summary>魔法防御</summary>
		public int Mgr;
		/// <summary>運</summary>
		public int Luc;
		/// <summary>回避率</summary>
		public int Agl;
		/// <summary>感情</summary>
		public SingltonSkillManager.Feel Feeling;
		/// <summary>感情値(仮)一定以上の感情値で技を覚えるため値を保持する</summary>
		public int FeelingValue;


	}

	/// <summary>
	/// 装備情報
	/// 変動する情報を保存用
	/// </summary>
	[Serializable]
	public class EquipmentParam {
		/// <summary>プレイヤー識別ID(0からの連番で管理)</summary>
		public string ID;
		/// <summary>武器(装備名)</summary>
		public string Arms;
		/// <summary>頭(装備名)</summary>
		public string Head;
		/// <summary> 鎧（装備名)</summary>
        public string Armor;
		/// <summary>足(装備名)</summary>
		public string Shoes;
		/// <summary>アクセサリー1(装備名)</summary>
		public string Accessory1;
		/// <summary>アクセサリー2(装備名)</summary>
		public string Accessory2;


	}

	/// <summary>
	/// スキル情報
	/// </summary>
	[Serializable]
	public class SkillParam {
		/// <summary>現在覚えているスキルの名前</summary>
		public List<string> Name;


	}

	/// <summary>
	/// アイテム情報
	/// </summary>
	[Serializable]
	public class ItemParam {
		public Dictionary<string,int> itemList;
		/// <summary>所持金</summary>
		public int possessionGolds;


	}

	/// <summary>
	/// イベント管理クラス
	/// </summary>
	[Serializable]
	public class FlagManage {
		/// <summary>イベントキー名, イベントフラグで管理するディクショナリ</summary>
		public Dictionary<string, bool> EventFlag;


	}

	/*===============================================================*/
	/// <summary>
	/// プレイヤー出現管理クラス
	/// </summary>
	[System.Serializable]
	public class SpawnScene {
		/// <summary>現在のプレイヤー出現場所(SceneName指定)</summary>
		public string currentSpawnPlayer;


	}
	/*===============================================================*/

	// パラメーター部 END
	/*===============================================================*/


}