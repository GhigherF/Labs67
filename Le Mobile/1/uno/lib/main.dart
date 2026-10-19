import 'dart:convert';
import 'dart:async';
//region lab2

mixin SpicyMixin {
  int scovilleUnits = 0;

  void printSpiciness() {
   print('Уровень остроты: $scovilleUnits SHU');
  }

  void setSpiciness(int scoville){
    scovilleUnits = scoville;
  }

  void addSpicySauce(String sauceName, {int scovilleBoost = 1000}) {
    scovilleUnits += scovilleBoost;
    print('Добавлен соус "$sauceName" (+$scovilleBoost SHU). Текущая острота: $scovilleUnits SHU');
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
  bool isHalal= false;
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
  List<Food> toList({bool growable = true}) => List.of(_foods, growable: growable);

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
        throw FormatException('Неизвестный тип объекта для десериализации: $type');
    }
  }

  @override
  int compareTo(Food other) {
    return _weightGrams.compareTo(other._weightGrams);
  }

  Food(this.name, double weightGrams) : _weightGrams = weightGrams > 0 ? weightGrams : 100.0 {
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

  void applySpecialOffer(
      [String? sauce,
        double discount = 0.0,
        void Function(String message)? logger]
      ) {
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

  void applySpecialOffer2(
      {String? sauce,
        double discount = 0.0,
        void Function(String message)? logger}
      ) {
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
    return BigInt.parse("FATASS".codeUnits.map((code) => code.toString()).join());
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
    return {
      'type': 'Shawa',
      'name': name,
      'weightGrams': weightGrams,
    };
  }

  factory Shawa.fromJson(Map<String, dynamic> json) {
    return Shawa(
      json['name'] as String,
      (json['weightGrams'] as num).toDouble(),
    );
  }

  @override
  final BigInt calories100g = BigInt.from(540);
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
Future<String> prepareOrderAsync(String name, {bool hasIngredient = true}) async {
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
  //region 2-4
  print("Бургер");
  final burger = Burger('Трёхфунтовый с сыром', 541);

  burger.serveMsg();


  print('Халяль: ${burger.isHalal ? "да" : "нет"}');
  print('Калорийность 100г: ${burger.calories100g}');
  print('Калорий в порции (FATASS BURGER): ${burger.calculateTotalCalories(
      burger.weightGrams)}');

  print('\nСУП');
  final borsch = Soup('Борщ', 451);

  borsch.serveMsg();
  borsch.setSpiciness(150);
  borsch.printSpiciness();
  print('Халяль: ${borsch.isHalal ? "да" : "нет"}');
  print(
      'Калорий в порции: ${borsch.calculateTotalCalories(borsch.weightGrams)}');

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

  Map<String, Burger> burgerMenu = {
    "B1": burger1,
    "B2": burger2,
  };
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
    }
    catch (e) {
      print("ERoRrrR:::: ${e}");
    }
    finally {
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
    print('"${burger.name}" (${burger.weightGrams}г) легче, чем "${shawa
        .name}" (${shawa.weightGrams}г)');
  } else if (burger.compareTo(shawa) > 0) {
    print('"${burger.name}" (${burger.weightGrams}г) тяжелее, чем "${shawa
        .name}" (${shawa.weightGrams}г)');
  } else {
    print('"${burger.name}" и "${shawa.name}" равны по весу');
  }

  List<Food> existingFoods = [burger, shawa, borsch];
  print('\n${existingFoods
      .map((f) => "${f.name} (${f.weightGrams}г)")
      .toList()}');

  existingFoods.sort();
  print('Sort: ${existingFoods
      .map((f) => "${f.name} (${f.weightGrams}г)")
      .toList()}');



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
  print('Восстановлен объект: ${restoredBurger.name} (${restoredBurger
      .runtimeType})');
  restoredBurger.serveMsg();

  final Map<String, dynamic> decodedShawaMap = jsonDecode(shawaJsonStr);
  final Food restoredShawa = Food.fromJson(decodedShawaMap);
  print('Восстановлен объект: ${restoredShawa.name} (${restoredShawa
      .runtimeType})');
  restoredShawa.serveMsg();
  await burger.serveMsg();
  await borsch.serveMsg();
  await shawa.serveMsg();


  await demoFuture();
  await demoStream();
//endregion

}

