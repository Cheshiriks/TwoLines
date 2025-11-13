mergeInto(LibraryManager.library, {

  GetStart: function () {
    ysdk.features.LoadingAPI.ready()
    console.log('Start Api');
  },
  
  GetLang: function () {
    var lang = ysdk.environment.i18n.lang;
    var bufferSize = lengthBytesUTF8(lang) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(lang, buffer, bufferSize);
    return buffer;
  },

  GetDomen: function () {
    var tld = ysdk.environment.i18n.tld;
    var bufferSize = lengthBytesUTF8(tld) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(tld, buffer, bufferSize);
    return buffer;
  },
  
    ShowFirstAdv: function () {
      ysdk.adv.showFullscreenAdv({
        callbacks: {
          onClose: function(wasShown) {
            myGameInstance.SendMessage('GameSound', 'Play');
          },
          onError: function(error) {
            myGameInstance.SendMessage('GameSound', 'Play');
          }
        }
      })
    },
  
  ShowAdv: function () {
    ysdk.adv.showFullscreenAdv({
      callbacks: {
        onClose: function(wasShown) {
          myGameInstance.SendMessage('GameMenuManager', 'AdContinue');
        },
        onError: function(error) {
          myGameInstance.SendMessage('GameMenuManager', 'AdContinue');
        }
      }
    })
  },
  
  ShowAdvMenu: function () {
    ysdk.adv.showFullscreenAdv({
      callbacks: {
        onClose: function(wasShown) {
          myGameInstance.SendMessage('GameMenuManager', 'AdMenu');
        },
        onError: function(error) {
          myGameInstance.SendMessage('GameMenuManager', 'AdMenu');
        }
      }
    })
  },  
  
  ShowVideoAdv: function () {
    ysdk.adv.showRewardedVideo({
      callbacks: {
        onOpen: () => {
          console.log('Video ad open.');
          myGameInstance.SendMessage('GameSound', 'Pause');
        },
        onRewarded: () => {
          console.log('Rewarded!');
          myGameInstance.SendMessage('Level', 'GetClue');
        },
        onClose: () => {
          console.log('Video ad closed.');
          myGameInstance.SendMessage('GameSound', 'PlayAndYandex');
        },
        onError: (e) => {
          console.log('Error while open video ad:', e);
          myGameInstance.SendMessage('GameSound', 'PlayAndYandex');
        }
      }
    })
  },

  GameplayStart: function () {
    ysdk.features.GameplayAPI.start()
    console.log('Gameplay Start');
  },

  GameplayStop: function () {
    ysdk.features.GameplayAPI.stop()
    console.log('Gameplay Stop');
  },
  
});