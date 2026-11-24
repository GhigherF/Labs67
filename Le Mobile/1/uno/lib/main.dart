// Сущности ЛР №2: Food (name, weightGrams), Burger, Soup и Shawa.
// Для блюд также используются свойства Calories.calories100g и Halal.isHalal;
// Burger и Soup содержат scovilleUnits. Menu хранит коллекцию Food.
import 'dart:io';
import 'dart:isolate';
import 'dart:convert';
import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:path/path.dart' as path;
import 'package:path_provider/path_provider.dart';
import 'package:sqflite/sqflite.dart';

//region lab2

mixin SpicyMixin {
  int scovilleUnits = 0;

  void printSpiciness() {
    print('Уровень остроты: $scovilleUnits SHU');
  }

  void setSpiciness(int scoville) {
    scovilleUnits = scoville;
  }

  void addSpicySauce(String sauceName, {int scovilleBoost = 1000}) {
    scovilleUnits += scovilleBoost;
    print(
      'Добавлен соус "$sauceName" (+$scovilleBoost SHU). Текущая острота: $scovilleUnits SHU',
    );
  }
}

abstract class Calories {
  BigInt get calories100g;

  BigInt calculateTotalCalories(double weightGrams) {
    double total = (calories100g.toDouble() * weightGrams) / 100;
    return BigInt.from(total.round());
  }
}

class Halal {
  bool isHalal = false;
}

class Menu implements Iterable<Food>, Iterator<Food> {
  final List<Food> _foods = [];
  int _currentIndex = -1;

  void add(Food food) {
    _foods.add(food);
  }

  @override
  Food get current {
    if (_currentIndex < 0 || _currentIndex >= _foods.length) {
      throw RangeError('Итератор находится за пределами коллекции');
    }
    return _foods[_currentIndex];
  }

  @override
  bool moveNext() {
    if (_currentIndex + 1 < _foods.length) {
      _currentIndex++;
      return true;
    }
    _currentIndex = -1;
    return false;
  }

  @override
  Iterator<Food> get iterator => this;

  //region Бесполезность
  @override
  int get length => _foods.length;

  @override
  bool get isEmpty => _foods.isEmpty;

  @override
  bool get isNotEmpty => _foods.isNotEmpty;

  @override
  Food get first => _foods.first;

  @override
  Food get last => _foods.last;

  @override
  Food get single => _foods.single;

  @override
  bool contains(Object? element) => _foods.contains(element);

  @override
  List<Food> toList({bool growable = true}) =>
      List.of(_foods, growable: growable);

  @override
  Set<Food> toSet() => _foods.toSet();

  @override
  noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
  //endregion
}

abstract class Food implements Comparable<Food> {
  final String name;
  double _weightGrams;
  static int totalCreatedCount = 0;

  Map<String, dynamic> toJson();

  static Food fromJson(Map<String, dynamic> json) {
    final type = json['type'] as String?;
    switch (type) {
      case 'Burger':
        return Burger.fromJson(json);
      case 'Soup':
        return Soup.fromJson(json);
      case 'Shawa':
        return Shawa.fromJson(json);
      default:
        throw FormatException(
          'Неизвестный тип объекта для десериализации: $type',
        );
    }
  }

  @override
  int compareTo(Food other) {
    return _weightGrams.compareTo(other._weightGrams);
  }

  Food(this.name, double weightGrams)
    : _weightGrams = weightGrams > 0 ? weightGrams : 100.0 {
    totalCreatedCount++;
  }

  double get weightGrams => _weightGrams;
  set weightGrams(double value) {
    if (value > 0) {
      _weightGrams = value;
    } else {
      print('Ошибка: вес блюда "$name" должен быть больше 0!');
    }
  }

  static void printTotalSummary() {
    print('>>> Всего приготовлено блюд в системе: $totalCreatedCount <<<');
  }

  Future<void> serveMsg();

  void applySpecialOffer([
    String? sauce,
    double discount = 0.0,
    void Function(String message)? logger,
  ]) {
    String info = 'Акция для "$name": скидка ${discount}%';
    if (sauce != null) {
      info += ', соус: $sauce';
    }

    if (logger != null) {
      logger(info);
    } else {
      print(info);
    }
  }

  void applySpecialOffer2({
    String? sauce,
    double discount = 0.0,
    void Function(String message)? logger,
  }) {
    String info = 'Акция для "$name": скидка ${discount}%';
    if (sauce != null) {
      info += ', соус: $sauce';
    }

    if (logger != null) {
      logger(info);
    } else {
      print(info);
    }
  }
}

class Burger extends Food with SpicyMixin implements Calories, Halal {
  @override
  Future<void> serveMsg() async {
    print('Готовим бургер "$name"...');
    await Future.delayed(Duration(seconds: 2));
    print(' Подан бургер "$name"');
  }

  @override
  Map<String, dynamic> toJson() {
    return {
      'type': 'Burger',
      'name': name,
      'weightGrams': weightGrams,
      'scovilleUnits': scovilleUnits,
    };
  }

  factory Burger.fromJson(Map<String, dynamic> json) {
    final burger = Burger(
      json['name'] as String,
      (json['weightGrams'] as num).toDouble(),
    );
    burger.scovilleUnits = (json['scovilleUnits'] as int?) ?? 0;
    return burger;
  }

  @override
  var isHalal = false;
  @override
  BigInt calories100g = BigInt.from(900);

  Burger(super.name, super.weightGrams);
  Burger.beer(String name) : super('$name + 2 литра пиваса', 2650) {
    isHalal = true;
  }

  @override
  BigInt calculateTotalCalories(double weightGrams) {
    return BigInt.parse(
      "FATASS".codeUnits.map((code) => code.toString()).join(),
    );
  }
}

class Soup extends Food with SpicyMixin implements Calories, Halal {
  @override
  Future<void> serveMsg() async {
    print('Готовим бурмалду "$name"...');
    await Future.delayed(Duration(seconds: 2));
    print(' Подан бургер "$name"');
  }

  @override
  Map<String, dynamic> toJson() {
    return {
      'type': 'Soup',
      'name': name,
      'weightGrams': weightGrams,
      'scovilleUnits': scovilleUnits,
    };
  }

  factory Soup.fromJson(Map<String, dynamic> json) {
    final soup = Soup(
      json['name'] as String,
      (json['weightGrams'] as num).toDouble(),
    );
    soup.scovilleUnits = (json['scovilleUnits'] as int?) ?? 0;
    return soup;
  }

  @override
  var isHalal = true;
  @override
  BigInt calories100g = BigInt.from(250);

  Soup(super.name, super.weightGrams);

  @override
  BigInt calculateTotalCalories(double weightGrams) {
    return BigInt.from((calories100g.toDouble() / 100 * weightGrams).round());
  }
}

class Shawa extends Food implements Calories, Halal {
  @override
  Future<void> serveMsg() async {
    print('Крутим шаурму "$name"...');
    await Future.delayed(Duration(seconds: 1));
    print('Сочная, как спелый арбуз $name подана!');
  }

  @override
  Map<String, dynamic> toJson() {
    return {'type': 'Shawa', 'name': name, 'weightGrams': weightGrams};
  }

  factory Shawa.fromJson(Map<String, dynamic> json) {
    return Shawa(
      json['name'] as String,
      (json['weightGrams'] as num).toDouble(),
    );
  }

  @override
  BigInt calories100g = BigInt.from(540);
  @override
  bool isHalal = true;

  Shawa(super.name, super.weightGrams);

  @override
  BigInt calculateTotalCalories(double weightGrams) {
    return BigInt.from((calories100g.toDouble() / 100 * weightGrams).round());
  }
}

Stream<String> generateOrdersStream() async* {
  yield 'Бургер';
  await Future.delayed(Duration(milliseconds: 300));
  yield 'Суп';
  await Future.delayed(Duration(milliseconds: 300));
  yield 'Шаурма';
  await Future.delayed(Duration(milliseconds: 300));
  yield 'Пицца';
}

Future<void> demoStream() async {
  final singleStream = generateOrdersStream();

  final transformedStream = singleStream
      .where((order) => order.length > 3)
      .map((order) => 'ЗАКАЗ: ${order.toUpperCase()}');

  StreamSubscription<String>? subscription;

  subscription = transformedStream.listen(
    (data) {
      print(' Однопоточный слушатель: $data');
      if (data.contains('ШАУРМА')) {
        print(' Отмена подписки (получена Шаурма)');
        subscription?.cancel();
      }
    },
    onDone: () => print(' Однопоточный поток завершен.\n'),
    onError: (err) => print(' Ошибка потока: $err'),
  );

  await Future.delayed(Duration(seconds: 2));

  print('--- Broadcast Stream ---');
  final controller = StreamController<String>.broadcast();

  final sub1 = controller.stream.listen((order) {
    print('Повар получил: $order');
  });

  final sub2 = controller.stream.listen((order) {
    print(' На табло отображено: $order');
  });

  controller.add('Чизбургер');
  await Future.delayed(Duration(milliseconds: 200));
  controller.add('Борщ');
  await Future.delayed(Duration(milliseconds: 200));

  await sub1.cancel();
  await sub2.cancel();
  await controller.close();
  print(' Broadcast поток закрыт.');
}

Future<String> prepareOrderAsync(
  String name, {
  bool hasIngredient = true,
}) async {
  print(' Начинаем готовить "$name"...');
  await Future.delayed(Duration(seconds: 1));

  if (!hasIngredient) {
    throw Exception('Ингредиенты для "$name" закончились!');
  }

  return 'Блюдо "$name" успешно приготовлено!';
}

Future<void> demoFuture() async {
  try {
    final result = await prepareOrderAsync('Бургер');
    print('[await]: $result\n');
  } catch (e) {
    print('[await error]: $e\n');
  }

  try {
    await prepareOrderAsync('Суп', hasIngredient: false);
  } catch (e) {
    print(' [catched error]: $e\n');
  }
  prepareOrderAsync('Шаурма')
      .then((res) {
        print(' Шаг 1: $res');
        return 'Упаковать шаурму в крафт-пакет';
      })
      .then((step2) {
        print(' Шаг 2: $step2');
      })
      .catchError((err) {
        print(' Ошибка в цепочке: $err');
      })
      .whenComplete(() {
        print(' [whenComplete]: Цепочка завершена.\n');
      });

  await Future.delayed(Duration(seconds: 2));
}

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const MyApp());

  //region 2-4
  print("Бургер");
  final burger = Burger('Трёхфунтовый с сыром', 541);

  burger.serveMsg();

  print('Халяль: ${burger.isHalal ? "да" : "нет"}');
  print('Калорийность 100г: ${burger.calories100g}');
  print(
    'Калорий в порции (FATASS BURGER): ${burger.calculateTotalCalories(burger.weightGrams)}',
  );

  print('\nСУП');
  final borsch = Soup('Борщ', 451);

  borsch.serveMsg();
  borsch.setSpiciness(150);
  borsch.printSpiciness();
  print('Халяль: ${borsch.isHalal ? "да" : "нет"}');
  print(
    'Калорий в порции: ${borsch.calculateTotalCalories(borsch.weightGrams)}',
  );

  print('\nТЕСТИРУЕМ ШАУРМУ');
  final shawa = Shawa('Шаурма с курицей', 301);

  shawa.serveMsg();
  print('Халяль: ${shawa.isHalal ? "да" : "нет"}');
  print('Калорий в порции: ${shawa.calculateTotalCalories(shawa.weightGrams)}');
  //endregion
  //region 5
  final beerBurger = Burger.beer('дефолтный бургер');
  beerBurger.serveMsg();
  print('Вес с пивасом: ${beerBurger.weightGrams}г');
  print('Халяль: ${beerBurger.isHalal ? "да" : "нет"}');

  print('Текущий вес супа: ${borsch.weightGrams}г');
  borsch.weightGrams = 500;
  print('Новый вес супа: ${borsch.weightGrams}г');
  borsch.weightGrams = -50;

  print(Food.totalCreatedCount);
  Food.printTotalSummary();

  borsch.applySpecialOffer();
  shawa.applySpecialOffer('Чесночный');
  shawa.applySpecialOffer('Острый', 15.0);
  burger.applySpecialOffer(
    'Сырный',
    25.0,
    (String msg) => print('[СПЕЦИАЛЬНЫЙ ЛОГ]: $msg'),
  );
  shawa.applySpecialOffer2(sauce: 'Острый', discount: 15.0);
  //endregion
  //region 6.1
  var list1 = [1, 2, 3];
  List<String> lols = ["Lol", "Kek", "Cheburek"];
  print(list1.length);
  print(list1.reversed);
  print(list1.reduce((sum, el) => sum += el));
  print(lols.join());
  print(lols[1]);
  print(lols.isEmpty);

  var burger1 = Burger("brgr1", 250);
  var burger2 = Burger("brgr2", 200);
  var burger3 = Burger("brgr3", 450);

  List<Burger> burgers = [burger1, burger2];
  burgers.add(burger3);
  print(burgers);
  burgers.remove(burgers[1]);
  print(burgers);
  //endregion
  //region 6.2
  Set<int> set1 = {1, 2, 3};
  Set<String> tags = {"Фастфуд", "Острое", "Халяль"};

  print(set1.length);
  print(tags.contains("Фастфуд"));
  print(tags.isEmpty);

  tags.add("Острое");
  print(tags);

  Set<Burger> burgerSet = {burger1, burger2};
  burgerSet.add(burger3);
  burgerSet.add(burger1);
  print(burgerSet);

  burgerSet.remove(burger2);
  print(burgerSet);
  //endregion
  //region 6.3
  Map<String, int> prices = {"Бургер": 450, "Суп": 320, "Шаурма": 280};

  print(prices.length);
  print(prices.keys);
  print(prices.values);
  print(prices["Бургер"]);
  print(prices.containsKey("Суп"));
  print(prices.isEmpty);

  prices["Пиво"] = 150;
  prices["Бургер"] = 500;
  print(prices);

  Map<String, Burger> burgerMenu = {"B1": burger1, "B2": burger2};
  burgerMenu["B3"] = burger3;
  print(burgerMenu);

  burgerMenu.remove("B2");
  print(burgerMenu);
  //endregion
  //region 7
  for (var i = 1; i < 1000; i++) {
    if (i % 2 == 0) continue;
    if (i == 15) break;
    print(i);
  }

  var i = 1;
  while (i < 1000) {
    if (i == 15) break;
    if (i % 2 == 0) {
      i++;
      continue;
    }

    print(i);
    i++;
  }

  var j = 1;
  do {
    if (j == 15) break;
    if (j % 2 == 0) {
      j++;
      continue;
    }
    print(j);
    j++;
  } while (j < 1000);

  for (var el in burgerSet) {
    if (el.isHalal) continue;
    print(el);
  }
  //endregion
  //region 8
  do {
    try {
      //throw "LoL";
      int? gg = null;
      print(gg! + 5);
    } catch (e) {
      print("ERoRrrR:::: ${e}");
    } finally {
      print("le Finale");
    }
    try {
      List<Food> menu = [burger, borsch];
      print(menu[99]);
    } catch (e) {
      print("ERoRrrR:::: $e");
    } finally {
      print("le Finale 2");
    }

    try {
      BigInt.parse("ХИХИХИХАХАХА");
    } catch (e) {
      print("ERoRrrR:::: $e");
    } finally {
      print("le Finale 3");
    }
  } while (1 == 2);
  //endregion

  //region lab3
  print('\n' * 100);

  burger.printSpiciness();
  burger.setSpiciness(500);
  //var burger2 = SpicyMixin();
  burger.printSpiciness();
  burger.addSpicySauce('Халапеньо', scovilleBoost: 1500);
  burger.printSpiciness();

  var menu = Menu();
  menu.add(burger);
  menu.add(borsch);
  menu.add(shawa);

  while (menu.moveNext()) {
    print(' ${menu.current.name}');
  }

  for (final food in menu) {
    print(' ${food.name}');
  }

  if (burger.compareTo(shawa) < 0) {
    print(
      '"${burger.name}" (${burger.weightGrams}г) легче, чем "${shawa.name}" (${shawa.weightGrams}г)',
    );
  } else if (burger.compareTo(shawa) > 0) {
    print(
      '"${burger.name}" (${burger.weightGrams}г) тяжелее, чем "${shawa.name}" (${shawa.weightGrams}г)',
    );
  } else {
    print('"${burger.name}" и "${shawa.name}" равны по весу');
  }

  List<Food> existingFoods = [burger, shawa, borsch];
  print(
    '\n${existingFoods.map((f) => "${f.name} (${f.weightGrams}г)").toList()}',
  );

  existingFoods.sort();
  print(
    'Sort: ${existingFoods.map((f) => "${f.name} (${f.weightGrams}г)").toList()}',
  );

  final burgerMap = burger.toJson();
  final String burgerJsonStr = jsonEncode(burgerMap);
  print('Объект burger в формате JSON:');
  print('  $burgerJsonStr');

  final shawaMap = shawa.toJson();
  final String shawaJsonStr = jsonEncode(shawaMap);
  print('Объект shawa в формате JSON:');
  print('  $shawaJsonStr');

  print('\n--- Восстановление объектов из JSON ---');

  final Map<String, dynamic> decodedBurgerMap = jsonDecode(burgerJsonStr);
  final Food restoredBurger = Food.fromJson(decodedBurgerMap);
  print(
    'Восстановлен объект: ${restoredBurger.name} (${restoredBurger.runtimeType})',
  );
  restoredBurger.serveMsg();

  final Map<String, dynamic> decodedShawaMap = jsonDecode(shawaJsonStr);
  final Food restoredShawa = Food.fromJson(decodedShawaMap);
  print(
    'Восстановлен объект: ${restoredShawa.name} (${restoredShawa.runtimeType})',
  );
  restoredShawa.serveMsg();
  await burger.serveMsg();
  await borsch.serveMsg();
  await shawa.serveMsg();

  await demoFuture();
  await demoStream();
  //endregion

  //region lab7

  //endregion
}

class MyApp extends StatelessWidget {
  final FoodRepository? repository;

  const MyApp({super.key, this.repository});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      home: Page1(repository: repository ?? SqfliteFoodRepository.instance),
    );
  }
}

class Page1 extends StatefulWidget {
  final FoodRepository repository;

  const Page1({super.key, required this.repository});

  @override
  State<Page1> createState() => _Page1State();
}

class _Page1State extends State<Page1> {
  final TextEditingController _searchController = TextEditingController();
  final List<FoodSort> _sorts = [FoodSort(SortField.name)];
  List<FoodEntry> _foods = [];
  int _loadRequest = 0;
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadFoods();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadFoods() async {
    final request = ++_loadRequest;
    setState(() {
      _isLoading = true;
      _error = null;
    });
    try {
      final foods = await widget.repository.loadFoods(
        query: _searchController.text,
        sorts: _sorts,
      );
      if (!mounted || request != _loadRequest) return;
      setState(() {
        _foods = foods;
        _isLoading = false;
      });
    } catch (error) {
      if (!mounted || request != _loadRequest) return;
      setState(() {
        _error = error.toString();
        _isLoading = false;
      });
    }
  }

  Future<void> _openForm([FoodEntry? entry]) async {
    final saved = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (context) =>
            FoodFormPage(repository: widget.repository, entry: entry),
      ),
    );
    if (saved == true) await _loadFoods();
  }

  Future<void> _openDetails(FoodEntry entry) async {
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (context) =>
            Page2(entry: entry, repository: widget.repository),
      ),
    );
    if (changed == true) await _loadFoods();
  }

  Future<void> _chooseSorts() async {
    final selectedSorts = await showDialog<List<FoodSort>>(
      context: context,
      builder: (context) => _SortDialog(initialSorts: _sorts),
    );
    if (selectedSorts == null || !mounted) return;
    setState(() {
      _sorts
        ..clear()
        ..addAll(selectedSorts);
    });
    await _loadFoods();
  }

  Future<void> _openFileSystem() async {
    await Navigator.of(context).push<void>(
      MaterialPageRoute(builder: (context) => const FileSystemPage()),
    );
  }

  @override
  Widget build(BuildContext context) {
    const backgroundColor = Color(0xFFF7F5F0);

    return Scaffold(
      backgroundColor: backgroundColor,
      appBar: AppBar(
        backgroundColor: backgroundColor,
        scrolledUnderElevation: 0,
        title: const Text(
          'Меню кухни',
          style: TextStyle(fontWeight: FontWeight.w700),
        ),
        actions: [
          IconButton(
            tooltip: 'Файловая система',
            onPressed: _openFileSystem,
            icon: const Icon(Icons.folder_open_outlined),
          ),
          IconButton(
            tooltip: 'Сортировка',
            onPressed: _chooseSorts,
            icon: const Icon(Icons.sort),
          ),
          Padding(
            padding: const EdgeInsets.only(right: 20),
            child: Center(
              child: Text(
                '${_foods.length} блюд',
                style: TextStyle(color: Colors.grey.shade700),
              ),
            ),
          ),
        ],
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Padding(
            padding: EdgeInsets.fromLTRB(20, 12, 20, 8),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Что приготовим?',
                  style: TextStyle(fontSize: 24, fontWeight: FontWeight.w700),
                ),
                SizedBox(height: 4),
                Text(
                  'Выберите блюдо, чтобы посмотреть его свойства',
                  style: TextStyle(color: Color(0xFF77736D)),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
            child: TextField(
              controller: _searchController,
              onChanged: (_) => _loadFoods(),
              decoration: InputDecoration(
                hintText: 'Поиск по названию или типу',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchController.text.isEmpty
                    ? null
                    : IconButton(
                        tooltip: 'Очистить поиск',
                        onPressed: () {
                          _searchController.clear();
                          _loadFoods();
                        },
                        icon: const Icon(Icons.clear),
                      ),
                filled: true,
                fillColor: Colors.white,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(16),
                  borderSide: BorderSide.none,
                ),
              ),
            ),
          ),
          Expanded(
            child: _isLoading
                ? const Center(child: CircularProgressIndicator())
                : _error != null
                ? _ErrorPanel(message: _error!, onRetry: _loadFoods)
                : _foods.isEmpty
                ? const Center(child: Text('Блюда не найдены'))
                : ListView.builder(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 20),
                    itemCount: _foods.length,
                    itemExtent: 124,
                    itemBuilder: (context, index) {
                      final entry = _foods[index];
                      final food = entry.food;
                      final color = _foodColor(food);

                      return Card(
                        margin: const EdgeInsets.symmetric(vertical: 6),
                        color: Colors.white,
                        elevation: 1,
                        shadowColor: Colors.black12,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(20),
                        ),
                        clipBehavior: Clip.antiAlias,
                        child: InkWell(
                          onTap: () => _openDetails(entry),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 14),
                            child: Row(
                              children: [
                                Container(
                                  width: 52,
                                  height: 52,
                                  decoration: BoxDecoration(
                                    color: color.withValues(alpha: 0.12),
                                    borderRadius: BorderRadius.circular(16),
                                  ),
                                  child: Icon(
                                    _foodIcon(food),
                                    color: color,
                                    size: 28,
                                  ),
                                ),
                                const SizedBox(width: 14),
                                Expanded(
                                  child: Column(
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        food.name,
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: const TextStyle(
                                          fontSize: 16,
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                      const SizedBox(height: 5),
                                      Text(
                                        _foodTypeName(food),
                                        style: TextStyle(
                                          color: color,
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        '${food.weightGrams.toStringAsFixed(0)} г'
                                        '  ·  ${_caloriesPer100g(food)} ккал / 100 г',
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: TextStyle(
                                          color: Colors.grey.shade600,
                                          fontSize: 12,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Icon(
                                  Icons.chevron_right,
                                  color: Colors.grey.shade500,
                                ),
                              ],
                            ),
                          ),
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _openForm(),
        tooltip: 'Добавить блюдо',
        child: const Icon(Icons.add),
      ),
    );
  }
}

class Page2 extends StatelessWidget {
  final FoodEntry entry;
  final FoodRepository repository;

  const Page2({super.key, required this.entry, required this.repository});

  @override
  Widget build(BuildContext context) {
    final food = entry.food;
    final color = _foodColor(food);
    final properties = <MapEntry<String, String>>[
      MapEntry('Тип блюда', _foodTypeName(food)),
      MapEntry('Вес порции', '${food.weightGrams.toStringAsFixed(0)} г'),
      if (food case final Calories calories)
        MapEntry('Калорийность на 100 г', '${calories.calories100g} ккал'),
      if (food case final Halal halal)
        MapEntry('Халяль', halal.isHalal ? 'Да' : 'Нет'),
      if (food case final SpicyMixin spicy)
        MapEntry('Острота', '${spicy.scovilleUnits} SHU'),
    ];

    return Scaffold(
      backgroundColor: const Color(0xFFF7F5F0),
      appBar: AppBar(
        backgroundColor: const Color(0xFFF7F5F0),
        scrolledUnderElevation: 0,
        title: const Text(
          'Информация о блюде',
          style: TextStyle(fontWeight: FontWeight.w700),
        ),
        actions: [
          IconButton(
            tooltip: 'Изменить',
            onPressed: () async {
              final saved = await Navigator.of(context).push<bool>(
                MaterialPageRoute(
                  builder: (context) =>
                      FoodFormPage(repository: repository, entry: entry),
                ),
              );
              if (saved == true && context.mounted) {
                Navigator.of(context).pop(true);
              }
            },
            icon: const Icon(Icons.edit_outlined),
          ),
          IconButton(
            tooltip: 'Удалить',
            onPressed: () async {
              final confirmed = await showDialog<bool>(
                context: context,
                builder: (context) => AlertDialog(
                  title: const Text('Удалить блюдо?'),
                  content: Text('«${food.name}» будет удалено из базы данных.'),
                  actions: [
                    TextButton(
                      onPressed: () => Navigator.pop(context, false),
                      child: const Text('Отмена'),
                    ),
                    FilledButton(
                      onPressed: () => Navigator.pop(context, true),
                      child: const Text('Удалить'),
                    ),
                  ],
                ),
              );
              if (confirmed != true || !context.mounted) return;
              try {
                await repository.deleteFood(entry.id);
                if (context.mounted) Navigator.of(context).pop(true);
              } catch (error) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(content: Text('Не удалось удалить блюдо: $error')),
                  );
                }
              }
            },
            icon: const Icon(Icons.delete_outline),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
        children: [
          Container(
            padding: const EdgeInsets.all(22),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(24),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 64,
                  height: 64,
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Icon(_foodIcon(food), color: color, size: 34),
                ),
                const SizedBox(height: 18),
                Text(
                  food.name,
                  style: const TextStyle(
                    fontSize: 24,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  'Свойства выбранной сущности меню',
                  style: TextStyle(color: Colors.grey.shade600),
                ),
              ],
            ),
          ),
          const SizedBox(height: 18),
          const Padding(
            padding: EdgeInsets.only(left: 4, bottom: 10),
            child: Text(
              'Характеристики',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
            ),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(20),
            ),
            child: Column(
              children: [
                for (var index = 0; index < properties.length; index++) ...[
                  _FoodPropertyRow(
                    label: properties[index].key,
                    value: properties[index].value,
                  ),
                  if (index != properties.length - 1)
                    Divider(height: 1, color: Colors.grey.shade200),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SortDialog extends StatefulWidget {
  final List<FoodSort> initialSorts;

  const _SortDialog({required this.initialSorts});

  @override
  State<_SortDialog> createState() => _SortDialogState();
}

class _SortDialogState extends State<_SortDialog> {
  late final List<FoodSort> _sorts = List.of(widget.initialSorts);

  @override
  Widget build(BuildContext context) {
    final availableFields = SortField.values
        .where((field) => !_sorts.any((sort) => sort.field == field))
        .toList();

    return AlertDialog(
      title: const Text('Сортировка'),
      content: SizedBox(
        width: 400,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var index = 0; index < _sorts.length; index++)
              Row(
                children: [
                  Expanded(
                    child: DropdownButton<SortField>(
                      isExpanded: true,
                      value: _sorts[index].field,
                      items: SortField.values
                          .where(
                            (field) =>
                                field == _sorts[index].field ||
                                !_sorts.asMap().entries.any(
                                  (entry) =>
                                      entry.key != index &&
                                      entry.value.field == field,
                                ),
                          )
                          .map(
                            (field) => DropdownMenuItem(
                              value: field,
                              child: Text(field.label),
                            ),
                          )
                          .toList(),
                      onChanged: (field) {
                        if (field == null) return;
                        setState(
                          () => _sorts[index] = _sorts[index].copyWith(
                            field: field,
                          ),
                        );
                      },
                    ),
                  ),
                  IconButton(
                    tooltip: _sorts[index].ascending
                        ? 'По возрастанию'
                        : 'По убыванию',
                    onPressed: () => setState(
                      () => _sorts[index] = _sorts[index].copyWith(
                        ascending: !_sorts[index].ascending,
                      ),
                    ),
                    icon: Icon(
                      _sorts[index].ascending
                          ? Icons.arrow_upward
                          : Icons.arrow_downward,
                    ),
                  ),
                  IconButton(
                    tooltip: 'Переместить выше',
                    onPressed: index == 0
                        ? null
                        : () => setState(() {
                            final current = _sorts.removeAt(index);
                            _sorts.insert(index - 1, current);
                          }),
                    icon: const Icon(Icons.keyboard_arrow_up),
                  ),
                  IconButton(
                    tooltip: 'Переместить ниже',
                    onPressed: index == _sorts.length - 1
                        ? null
                        : () => setState(() {
                            final current = _sorts.removeAt(index);
                            _sorts.insert(index + 1, current);
                          }),
                    icon: const Icon(Icons.keyboard_arrow_down),
                  ),
                  IconButton(
                    tooltip: 'Удалить поле сортировки',
                    onPressed: _sorts.length == 1
                        ? null
                        : () => setState(() => _sorts.removeAt(index)),
                    icon: const Icon(Icons.remove_circle_outline),
                  ),
                ],
              ),
            if (availableFields.isNotEmpty)
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton.icon(
                  onPressed: () => setState(
                    () => _sorts.add(FoodSort(availableFields.first)),
                  ),
                  icon: const Icon(Icons.add),
                  label: const Text('Добавить поле'),
                ),
              ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Отмена'),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(context, _sorts),
          child: const Text('Применить'),
        ),
      ],
    );
  }
}

class _ErrorPanel extends StatelessWidget {
  final String message;
  final Future<void> Function() onRetry;

  const _ErrorPanel({required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 42, color: Colors.redAccent),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton.tonal(
              onPressed: onRetry,
              child: const Text('Повторить'),
            ),
          ],
        ),
      ),
    );
  }
}

class _FoodPropertyRow extends StatelessWidget {
  final String label;
  final String value;

  const _FoodPropertyRow({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 16),
      child: Row(
        children: [
          Expanded(
            child: Text(label, style: TextStyle(color: Colors.grey.shade700)),
          ),
          const SizedBox(width: 16),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}

String _foodTypeName(Food food) {
  if (food is Burger) return 'Бургер';
  if (food is Soup) return 'Суп';
  if (food is Shawa) return 'Шаурма';
  return 'Блюдо';
}

int _caloriesPer100g(Food food) {
  if (food case final Calories calories) {
    return calories.calories100g.toInt();
  }
  return 0;
}

IconData _foodIcon(Food food) {
  if (food is Burger) return Icons.lunch_dining;
  if (food is Soup) return Icons.soup_kitchen;
  return Icons.restaurant;
}

Color _foodColor(Food food) {
  if (food is Burger) return const Color(0xFFE7833B);
  if (food is Soup) return const Color(0xFF4D9A75);
  return const Color(0xFF6978C8);
}

enum SortField {
  name('Название', 'name'),
  type('Тип блюда', 'type'),
  weight('Вес порции', 'weightGrams'),
  calories('Калорийность', 'calories100g'),
  halal('Халяль', 'isHalal'),
  spiciness('Острота', 'scovilleUnits');

  final String label;
  final String column;

  const SortField(this.label, this.column);
}

class FoodSort {
  final SortField field;
  final bool ascending;

  const FoodSort(this.field, {this.ascending = true});

  FoodSort copyWith({SortField? field, bool? ascending}) {
    return FoodSort(
      field ?? this.field,
      ascending: ascending ?? this.ascending,
    );
  }
}

class FoodEntry {
  final int id;
  final Food food;

  const FoodEntry({required this.id, required this.food});
}

abstract interface class FoodRepository {
  Future<List<FoodEntry>> loadFoods({
    String query = '',
    List<FoodSort> sorts = const [],
  });

  Future<int> insertFood(Food food);

  Future<void> updateFood(int id, Food food);

  Future<void> deleteFood(int id);
}

class SqfliteFoodRepository implements FoodRepository {
  SqfliteFoodRepository._();

  static final SqfliteFoodRepository instance = SqfliteFoodRepository._();

  Future<Database>? _database;

  Future<Database> get _db async {
    final databaseFuture = _database ??= _openDatabase();
    try {
      return await databaseFuture;
    } catch (_) {
      _database = null;
      rethrow;
    }
  }

  Future<Database> _openDatabase() async {
    final databasePath = await getDatabasesPath();
    final database = await openDatabase(
      path.join(databasePath, 'lab2_foods.db'),
      version: 1,
      onCreate: (db, version) async {
        await db.execute('''
          CREATE TABLE foods (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            type TEXT NOT NULL,
            name TEXT NOT NULL,
            weightGrams REAL NOT NULL,
            calories100g INTEGER NOT NULL,
            isHalal INTEGER NOT NULL,
            scovilleUnits INTEGER NOT NULL,
            searchText TEXT NOT NULL
          )
        ''');
      },
    );
    await _seedIfEmpty(database);
    return database;
  }

  Future<void> _seedIfEmpty(Database database) async {
    await database.transaction((transaction) async {
      final count = Sqflite.firstIntValue(
        await transaction.rawQuery('SELECT COUNT(*) FROM foods'),
      );
      if (count != 0) return;

      final seedFoods = [
        Burger('Бургер с сыром', 320)
          ..calories100g = BigInt.from(900)
          ..isHalal = false
          ..setSpiciness(1200),
        Burger('Классический бургер', 280)
          ..calories100g = BigInt.from(900)
          ..isHalal = false
          ..setSpiciness(400),
        Soup('Борщ домашний', 350)
          ..calories100g = BigInt.from(250)
          ..isHalal = true
          ..setSpiciness(150),
        Soup('Крем-суп грибной', 300)
          ..calories100g = BigInt.from(250)
          ..isHalal = true
          ..setSpiciness(0),
        Shawa('Шаурма с курицей', 350),
        Shawa('Шаурма овощная', 300),
      ];
      for (final food in seedFoods) {
        await transaction.insert('foods', _foodToMap(food));
      }
    });
  }

  @override
  Future<List<FoodEntry>> loadFoods({
    String query = '',
    List<FoodSort> sorts = const [],
  }) async {
    final database = await _db;
    final normalizedQuery = query.trim();
    final orderBy = sorts.isEmpty
        ? 'name COLLATE NOCASE ASC, id ASC'
        : [
            ...sorts.map(
              (sort) =>
                  '${sort.field.column} ${sort.ascending ? 'ASC' : 'DESC'}',
            ),
            'id ASC',
          ].join(', ');
    final rows = await database.query(
      'foods',
      where: normalizedQuery.isEmpty ? null : 'searchText LIKE ?',
      whereArgs: normalizedQuery.isEmpty
          ? null
          : ['%${normalizedQuery.toLowerCase()}%'],
      orderBy: orderBy,
    );
    return rows.map(_entryFromMap).toList();
  }

  @override
  Future<int> insertFood(Food food) async {
    final database = await _db;
    return database.insert('foods', _foodToMap(food));
  }

  @override
  Future<void> updateFood(int id, Food food) async {
    final database = await _db;
    final updated = await database.update(
      'foods',
      _foodToMap(food),
      where: 'id = ?',
      whereArgs: [id],
    );
    if (updated != 1) throw StateError('Блюдо с id=$id не найдено.');
  }

  @override
  Future<void> deleteFood(int id) async {
    final database = await _db;
    final deleted = await database.delete(
      'foods',
      where: 'id = ?',
      whereArgs: [id],
    );
    if (deleted != 1) throw StateError('Блюдо с id=$id не найдено.');
  }
}

Map<String, Object?> _foodToMap(Food food) {
  return {
    'type': _foodTypeName(food),
    'name': food.name,
    'weightGrams': food.weightGrams,
    'calories100g': _caloriesPer100g(food),
    'isHalal': _foodIsHalal(food) ? 1 : 0,
    'scovilleUnits': _foodSpiciness(food),
    'searchText':
        '${food.name.toLowerCase()} ${_foodTypeName(food).toLowerCase()}',
  };
}

bool _foodIsHalal(Food food) {
  if (food case final Halal halal) return halal.isHalal;
  return false;
}

int _foodSpiciness(Food food) {
  if (food case final SpicyMixin spicy) return spicy.scovilleUnits;
  return 0;
}

FoodEntry _entryFromMap(Map<String, Object?> row) {
  final food = _foodFromMap(row);
  return FoodEntry(id: row['id']! as int, food: food);
}

Food _foodFromMap(Map<String, Object?> values) {
  final type = values['type']! as String;
  final name = values['name']! as String;
  final weight = (values['weightGrams']! as num).toDouble();
  final calories = BigInt.from((values['calories100g']! as num).toInt());
  final isHalal = (values['isHalal']! as num) != 0;
  final scoville = (values['scovilleUnits']! as num).toInt();
  final food = switch (type) {
    'Бургер' || 'Burger' => Burger(name, weight),
    'Суп' || 'Soup' => Soup(name, weight),
    'Шаурма' || 'Shawa' => Shawa(name, weight),
    _ => throw FormatException('Неизвестный тип блюда в базе: $type'),
  };

  if (food is Burger) {
    food
      ..calories100g = calories
      ..isHalal = isHalal
      ..setSpiciness(scoville);
  } else if (food is Soup) {
    food
      ..calories100g = calories
      ..isHalal = isHalal
      ..setSpiciness(scoville);
  } else if (food is Shawa) {
    food
      ..calories100g = calories
      ..isHalal = isHalal;
  }
  return food;
}

class FoodFormPage extends StatefulWidget {
  final FoodRepository repository;
  final FoodEntry? entry;

  const FoodFormPage({super.key, required this.repository, this.entry});

  @override
  State<FoodFormPage> createState() => _FoodFormPageState();
}

class _FoodFormPageState extends State<FoodFormPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _weightController;
  late final TextEditingController _caloriesController;
  late final TextEditingController _spicinessController;
  late String _type;
  late bool _isHalal;
  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    final food = widget.entry?.food;
    _type = food == null ? 'Burger' : _foodTypeCode(food);
    _nameController = TextEditingController(text: food?.name ?? '');
    _weightController = TextEditingController(
      text: food?.weightGrams.toStringAsFixed(0) ?? '300',
    );
    _caloriesController = TextEditingController(
      text: food == null ? '900' : _caloriesPer100g(food).toString(),
    );
    _spicinessController = TextEditingController(
      text: '${food == null ? 0 : _foodSpiciness(food)}',
    );
    _isHalal = food == null ? false : _foodIsHalal(food);
  }

  @override
  void dispose() {
    _nameController.dispose();
    _weightController.dispose();
    _caloriesController.dispose();
    _spicinessController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _isSaving = true);
    try {
      final food = _buildFood();
      final entry = widget.entry;
      if (entry == null) {
        await widget.repository.insertFood(food);
      } else {
        await widget.repository.updateFood(entry.id, food);
      }
      if (mounted) Navigator.of(context).pop(true);
    } catch (error) {
      if (mounted) {
        setState(() => _isSaving = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Не удалось сохранить блюдо: $error')),
        );
      }
    }
  }

  Food _buildFood() {
    final name = _nameController.text.trim();
    final weight = double.parse(_weightController.text);
    final calories = BigInt.parse(_caloriesController.text);
    final scoville = int.parse(_spicinessController.text);
    final food = switch (_type) {
      'Burger' => Burger(name, weight),
      'Soup' => Soup(name, weight),
      'Shawa' => Shawa(name, weight),
      _ => throw StateError('Неизвестный тип блюда: $_type'),
    };
    if (food is Burger) {
      food
        ..calories100g = calories
        ..isHalal = _isHalal
        ..setSpiciness(scoville);
    } else if (food is Soup) {
      food
        ..calories100g = calories
        ..isHalal = _isHalal
        ..setSpiciness(scoville);
    } else if (food is Shawa) {
      food
        ..calories100g = calories
        ..isHalal = _isHalal;
    }
    return food;
  }

  @override
  Widget build(BuildContext context) {
    final supportsSpiciness = _type != 'Shawa';

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.entry == null ? 'Новое блюдо' : 'Изменить блюдо'),
      ),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(20),
          children: [
            DropdownButtonFormField<String>(
              value: _type,
              decoration: const InputDecoration(labelText: 'Тип блюда'),
              items: const [
                DropdownMenuItem(value: 'Burger', child: Text('Бургер')),
                DropdownMenuItem(value: 'Soup', child: Text('Суп')),
                DropdownMenuItem(value: 'Shawa', child: Text('Шаурма')),
              ],
              onChanged: (type) {
                if (type == null) return;
                setState(() {
                  _type = type;
                  if (widget.entry == null) {
                    _caloriesController.text = switch (type) {
                      'Burger' => '900',
                      'Soup' => '250',
                      'Shawa' => '540',
                      _ => '0',
                    };
                    _isHalal = type != 'Burger';
                    _spicinessController.text = '0';
                  }
                });
              },
            ),
            TextFormField(
              controller: _nameController,
              decoration: const InputDecoration(labelText: 'Название'),
              textCapitalization: TextCapitalization.sentences,
              validator: (value) => value == null || value.trim().isEmpty
                  ? 'Введите название'
                  : null,
            ),
            TextFormField(
              controller: _weightController,
              decoration: const InputDecoration(labelText: 'Вес порции, г'),
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              validator: (value) {
                final number = double.tryParse(value ?? '');
                return number == null || number <= 0
                    ? 'Введите вес больше нуля'
                    : null;
              },
            ),
            TextFormField(
              controller: _caloriesController,
              decoration: const InputDecoration(
                labelText: 'Калорийность на 100 г',
              ),
              keyboardType: TextInputType.number,
              validator: (value) {
                final number = int.tryParse(value ?? '');
                return number == null || number < 0
                    ? 'Введите неотрицательное число'
                    : null;
              },
            ),
            if (supportsSpiciness)
              TextFormField(
                controller: _spicinessController,
                decoration: const InputDecoration(labelText: 'Острота, SHU'),
                keyboardType: TextInputType.number,
                validator: (value) {
                  final number = int.tryParse(value ?? '');
                  return number == null || number < 0
                      ? 'Введите неотрицательное число'
                      : null;
                },
              ),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Халяль'),
              value: _isHalal,
              onChanged: (value) => setState(() => _isHalal = value),
            ),
            const SizedBox(height: 24),
            FilledButton.icon(
              onPressed: _isSaving ? null : _save,
              icon: _isSaving
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.save_outlined),
              label: Text(widget.entry == null ? 'Добавить' : 'Сохранить'),
            ),
          ],
        ),
      ),
    );
  }
}

String _foodTypeCode(Food food) {
  if (food is Burger) return 'Burger';
  if (food is Soup) return 'Soup';
  if (food is Shawa) return 'Shawa';
  throw FormatException('Неизвестный класс блюда: ${food.runtimeType}');
}

class _DirectorySpec {
  final String key;
  final String label;
  final Set<TargetPlatform> supportedPlatforms;
  final Future<List<Directory?>> Function() resolve;

  const _DirectorySpec({
    required this.key,
    required this.label,
    required this.supportedPlatforms,
    required this.resolve,
  });
}

class _DirectoryResult {
  final _DirectorySpec spec;
  final List<Directory> directories;
  final List<String> files;
  final String? error;

  const _DirectoryResult({
    required this.spec,
    this.directories = const [],
    this.files = const [],
    this.error,
  });

  _DirectoryResult copyWith({
    List<Directory>? directories,
    List<String>? files,
    String? error,
  }) {
    return _DirectoryResult(
      spec: spec,
      directories: directories ?? this.directories,
      files: files ?? this.files,
      error: error,
    );
  }
}

class FileSystemPage extends StatefulWidget {
  const FileSystemPage({super.key});

  @override
  State<FileSystemPage> createState() => _FileSystemPageState();
}

class _FileSystemPageState extends State<FileSystemPage> {
  late final List<_DirectorySpec> _specs = _createDirectorySpecs();
  late List<_DirectoryResult> _results = _specs
      .map((spec) => _DirectoryResult(spec: spec))
      .toList();
  bool _isLoading = true;
  bool _isRunningIsolate = false;
  List<String> _readResults = [];

  @override
  void initState() {
    super.initState();
    _scanDirectories();
  }

  String get _platformName {
    if (Platform.isAndroid) return 'Android';
    if (Platform.isIOS) return 'iOS';
    if (Platform.isWindows) return 'Windows';
    if (Platform.isMacOS) return 'macOS';
    if (Platform.isLinux) return 'Linux';
    return 'неизвестной платформе';
  }

  Future<void> _scanDirectories() async {
    setState(() => _isLoading = true);
    final results = <_DirectoryResult>[];
    for (final spec in _specs) {
      if (!spec.supportedPlatforms.contains(defaultTargetPlatform)) {
        final supported = spec.supportedPlatforms
            .map(_platformNameForTarget)
            .join(', ');
        results.add(
          _DirectoryResult(
            spec: spec,
            error:
                'Не поддерживается на $_platformName. '
                'Доступно на: $supported.',
          ),
        );
        continue;
      }
      try {
        final baseDirectories = (await spec.resolve())
            .whereType<Directory>()
            .toList();
        if (baseDirectories.isEmpty) {
          throw UnsupportedError(
            'Система не вернула каталог для $_platformName.',
          );
        }
        final resolved = baseDirectories
            .map(
              (directory) =>
                  Directory(path.join(directory.path, 'uno_lab7_${spec.key}')),
            )
            .toList();
        final filePaths = <String>[];
        for (final directory in resolved) {
          try {
            await directory.create(recursive: true);
            await for (final entity in directory.list(followLinks: false)) {
              if (entity is File && path.extension(entity.path) == '.json') {
                filePaths.add(entity.path);
              }
            }
          } on FileSystemException catch (error) {
            filePaths.add('Ошибка чтения каталога ${directory.path}: $error');
          }
        }
        results.add(
          _DirectoryResult(spec: spec, directories: resolved, files: filePaths),
        );
      } catch (error) {
        results.add(
          _DirectoryResult(
            spec: spec,
            error:
                'Не удалось получить каталог «${spec.label}» '
                'на $_platformName: $error',
          ),
        );
      }
    }
    if (!mounted) return;
    setState(() {
      _results = results;
      _isLoading = false;
    });
  }

  Future<void> _runIsolateDemo() async {
    setState(() {
      _isRunningIsolate = true;
      _readResults = [];
    });
    final messages = <String>[];
    var fileIndex = 0;
    for (final result in _results) {
      if (result.directories.isEmpty) continue;
      final directory = result.directories.first;
      final food = _sampleFoodForDirectory(fileIndex, result.spec.label);
      fileIndex++;
      final file = File(
        path.join(directory.path, 'lab7_${result.spec.key}.json'),
      );
      try {
        await directory.create(recursive: true);
        final jsonText = jsonEncode(_foodToFileMap(food, result.spec.label));
        final readMap = await writeAndReadJsonFileInIsolate(
          file.path,
          jsonText,
        );
        final restoredFood = _foodFromMap(readMap);
        messages.add(
          '${result.spec.label}: ${_foodTypeName(restoredFood)} '
          '«${restoredFood.name}», '
          '${restoredFood.weightGrams.toStringAsFixed(0)} г, '
          '${_caloriesPer100g(restoredFood)} ккал/100 г, '
          'халяль: ${_foodIsHalal(restoredFood) ? 'да' : 'нет'}, '
          'острота: ${_foodSpiciness(restoredFood)} SHU; ${file.path}',
        );
      } catch (error) {
        messages.add(
          '${result.spec.label}: ошибка записи/чтения на $_platformName: $error',
        );
      }
    }

    await _scanDirectories();
    if (!mounted) return;
    setState(() {
      _readResults = messages;
      _isRunningIsolate = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Файловая система')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _scanDirectories,
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  const Text(
                    'Каталоги приложения',
                    style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Пути получены через path_provider. Ограничения платформы '
                    'показываются рядом с каждым каталогом.',
                    style: TextStyle(color: Colors.grey.shade700),
                  ),
                  const SizedBox(height: 12),
                  for (final result in _results) _DirectoryCard(result: result),
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: _isRunningIsolate ? null : _runIsolateDemo,
                    icon: _isRunningIsolate
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.sync_alt),
                    label: const Text(
                      'Записать и прочитать сущности в изолятах',
                    ),
                  ),
                  const SizedBox(height: 12),
                  if (_readResults.isNotEmpty) ...[
                    const Text(
                      'Результаты чтения',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    for (final result in _readResults)
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        leading: const Icon(Icons.description_outlined),
                        title: Text(result),
                      ),
                  ],
                ],
              ),
            ),
    );
  }
}

class _DirectoryCard extends StatelessWidget {
  final _DirectoryResult result;

  const _DirectoryCard({required this.result});

  @override
  Widget build(BuildContext context) {
    final error = result.error;
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 5),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              result.spec.label,
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 6),
            if (error != null)
              Text(error, style: const TextStyle(color: Colors.deepOrange))
            else ...[
              for (final directory in result.directories)
                SelectableText(directory.path),
              if (result.files.isEmpty)
                Text(
                  'JSON-файлы пока не найдены',
                  style: TextStyle(color: Colors.grey.shade600),
                )
              else ...[
                const SizedBox(height: 8),
                for (final file in result.files) SelectableText('Файл: $file'),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

List<_DirectorySpec> _createDirectorySpecs() {
  const android = {TargetPlatform.android};
  const androidAndIos = {TargetPlatform.android, TargetPlatform.iOS};
  const ios = {TargetPlatform.iOS};

  return [
    _DirectorySpec(
      key: 'temporary',
      label: 'Temporary',
      supportedPlatforms: androidAndIos,
      resolve: () async => [await getTemporaryDirectory()],
    ),
    _DirectorySpec(
      key: 'application_support',
      label: 'Application Support',
      supportedPlatforms: androidAndIos,
      resolve: () async => [await getApplicationSupportDirectory()],
    ),
    _DirectorySpec(
      key: 'application_library',
      label: 'Application Library',
      supportedPlatforms: ios,
      resolve: () async => [await getLibraryDirectory()],
    ),
    _DirectorySpec(
      key: 'application_documents',
      label: 'Application Documents',
      supportedPlatforms: androidAndIos,
      resolve: () async => [await getApplicationDocumentsDirectory()],
    ),
    _DirectorySpec(
      key: 'application_cache',
      label: 'Application Cache',
      supportedPlatforms: androidAndIos,
      resolve: () async => [await getApplicationCacheDirectory()],
    ),
    _DirectorySpec(
      key: 'external_storage',
      label: 'External Storage',
      supportedPlatforms: android,
      resolve: () async => [await getExternalStorageDirectory()],
    ),
    _DirectorySpec(
      key: 'external_cache_directories',
      label: 'External Cache Directories',
      supportedPlatforms: android,
      resolve: () async => await getExternalCacheDirectories() ?? [],
    ),
    _DirectorySpec(
      key: 'external_storage_directories',
      label: 'External Storage Directories',
      supportedPlatforms: android,
      resolve: () async => await getExternalStorageDirectories() ?? [],
    ),
    _DirectorySpec(
      key: 'downloads',
      label: 'Downloads',
      supportedPlatforms: androidAndIos,
      resolve: () async => [await getDownloadsDirectory()],
    ),
  ];
}

String _platformNameForTarget(TargetPlatform platform) {
  return switch (platform) {
    TargetPlatform.android => 'Android',
    TargetPlatform.iOS => 'iOS',
    TargetPlatform.macOS => 'macOS',
    TargetPlatform.windows => 'Windows',
    TargetPlatform.linux => 'Linux',
    TargetPlatform.fuchsia => 'Fuchsia',
  };
}

Food _sampleFoodForDirectory(int index, String directoryName) {
  final suffix = directoryName.replaceAll(' ', '-');
  switch (index % 3) {
    case 0:
      return Burger('Изолят: бургер ($suffix)', 250 + index * 10)
        ..calories100g = BigInt.from(700 + index * 10)
        ..isHalal = index.isEven
        ..setSpiciness(500 + index * 100);
    case 1:
      return Soup('Изолят: суп ($suffix)', 300 + index * 10)
        ..calories100g = BigInt.from(200 + index * 10)
        ..isHalal = true
        ..setSpiciness(index * 25);
    default:
      return Shawa('Изолят: шаурма ($suffix)', 320 + index * 10)
        ..calories100g = BigInt.from(450 + index * 10)
        ..isHalal = index.isEven;
  }
}

Map<String, Object?> _foodToFileMap(Food food, String directoryLabel) {
  return {..._foodToMap(food), 'directory': directoryLabel};
}

Future<Map<String, dynamic>> _writeAndReadJsonFile(
  String filePath,
  String contents,
) async {
  final file = File(filePath);
  await file.writeAsString(contents, flush: true);
  final decoded = jsonDecode(await file.readAsString());
  if (decoded is! Map<String, dynamic>) {
    throw const FormatException('Содержимое файла не является JSON-объектом.');
  }
  return decoded;
}

Future<Map<String, dynamic>> writeAndReadJsonFileInIsolate(
  String filePath,
  String contents,
) {
  return Isolate.run(() => _writeAndReadJsonFile(filePath, contents));
}
