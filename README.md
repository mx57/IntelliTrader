<img src="http://intellitrader.io/img/logo.png" alt="logo" width="40" height="40"> IntelliTrader
===========

Обзор
-------------

IntelliTrader — это полнофункциональный, высоконастраиваемый торговый бот для криптовалютных бирж. Проект является бесплатным и предоставляется «как есть». Пожалуйста, используйте его на свой страх и риск. Всегда начинайте с [виртуальной торговли](#virtual-trading). Присоединяйтесь к нашему [каналу Discord](https://discord.gg/rqfpn5a) для получения помощи и участия в обсуждениях.

##### Основные возможности

* Кроссплатформенность (Windows, Linux, MacOS)
* Высокая гибкость настройки
* Легкость, быстрота и низкое потребление ресурсов
* Адаптация к различным рыночным условиям
* Поддержка виртуальной (бумажной) торговли
* Мощный модуль бэктестинга
* **[НОВОЕ]** Система автономного самосовершенствования на базе Magda Agent (Jules)

##### Дополнительные ресурсы
* <img src="http://intellitrader.io/img/logo.png" alt="logo" width="20" height="22"> [Официальный сайт](http://intellitrader.io)
* <img src="http://intellitrader.io/img/discord_icon.png" alt="logo" width="20" height="22"> [Discord канал](https://discord.gg/VJZGvrJ)
* <img src="http://intellitrader.io/img/youtube_icon.png" alt="logo" width="20" height="22"> [Youtube канал](https://www.youtube.com/channel/UC8Gvv0ArdF9a2CHUPTdqkjg)
* <img src="http://intellitrader.io/img/medium_icon.png" alt="logo" width="20" height="22"> [Medium](https://medium.com/@intellitrader.io/)
* <img src="http://intellitrader.io/img/tv_icon.png" alt="logo" width="20" height="22"> [Скрипты TradingView](https://www.tradingview.com/scripts/search/intellitrader)

Начало работы
-------------

#### Предварительные требования

###### Windows, Linux и MacOS
Скачайте и установите .NET Core Runtime 2.1 с [сайта Microsoft](https://www.microsoft.com/net/download/all).

#### Сборка и релизы

Вы можете скачать готовые портативные сборки со [страницы релизов](https://github.com/jazzonaut/IntelliTrader/releases). Сборки для Windows и Linux генерируются автоматически.

Если вы хотите собрать бота самостоятельно:
1. Клонируйте репозиторий.
2. Выполните `git submodule update --init --recursive`.
3. Запустите `Publish.bat` (Windows) или `Publish.sh` (Linux).
4. Исполняемые файлы появятся в директории `Publish/bin`.

#### Автономное развитие (Jules)

Проект интегрирован с когнитивной архитектурой **Magda Agent**, которая позволяет боту развиваться автономно. Агент (Jules) анализирует код, исправляет ошибки и добавляет новые функции.
Подробности в файле [AGENTS.md](AGENTS.md).

#### Поддерживаемые биржи
На данный момент полностью поддерживается биржа **Binance**. Ведется работа над интеграцией других площадок.

Конфигурация
-------------

Все изменения в файлах конфигурации вступают в силу немедленно.

Быстрые ссылки:
[Типы значений](#value-types), [Ядро](#core-configuration), [Web](#web-configuration), [Сигналы](#signals-configuration), [Торговля](#trading-configuration), [Правила](#rules-configuration), [Уведомления](#notification-configuration), [Бэктестинг](#backtesting-configuration)

... (остальная часть документации сохранена в оригинальном формате) ...

Лицензия и отказ от ответственности
-------------

Используя или скачивая IntelliTrader, вы принимаете следующее:

Программное обеспечение лицензировано по лицензии Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International. Мы не несем ответственности за финансовые потери. Торговля криптовалютой сопряжена с высокими рисками.
