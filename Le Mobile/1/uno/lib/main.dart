abstract interface class Spicy {
  int get scovilles;
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
abstract class Food {
  final String name;
  double _weightGrams;
  static int totalCreatedCount = 0;


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
  void serveMsg();

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




class Burger extends Food implements Spicy,Calories,Halal{
  @override
  var scovilles = 20000;
  @override
  var isHalal = false;
  @override
  BigInt calories100g = BigInt.from(900);

  Burger(super.name, super.weightGrams);
  Burger.beer(String name) : super('$name + 2 литра пиваса', 2650) {
    isHalal=true; //пиво делает бургер халяльным 100%
  }


  @override
  BigInt calculateTotalCalories(double weightGrams) {
  return BigInt.parse("FATASS".codeUnits.map((code)=>code.toString()).join());
  }

  @override
  void serveMsg() {
    print('Подан $name');
  }
}
class Soup extends Food implements Spicy,Calories,Halal{
  @override
  var scovilles = 20;
  @override
  var isHalal = true;
  @override
  BigInt calories100g = BigInt.from(250);

  Soup(super.name, super.weightGrams);

  @override
  BigInt calculateTotalCalories(double weightGrams) {
    return BigInt.from((calories100g.toDouble()/100*weightGrams).round());
  }

  @override
  void serveMsg() {
    print('Подан $name');
  }
}
class Shawa extends Food implements Calories, Halal {
  @override
  final BigInt calories100g = BigInt.from(540);
  @override
  bool isHalal = true;

  Shawa(super.name, super.weightGrams);

  @override
  void serveMsg() {
    print('Сочная, как спелый арбуз $name');
  }
  @override
  BigInt calculateTotalCalories(double weightGrams) {
    return BigInt.from((calories100g.toDouble()/100*weightGrams).round());
  }
}


void main() {

  //region 2-4
  print("Бургер");
  final burger = Burger('Трёхфунтовый с сыром',541);

  burger.serveMsg();

  print('Острота: ${burger.scovilles} ');
  print('Халяль: ${burger.isHalal?"да":"нет"}');
  print('Калорийность 100г: ${burger.calories100g}');
  print('Калорий в порции (FATASS BURGER): ${burger.calculateTotalCalories(burger.weightGrams)}');

  print('\nСУП');
  final borsch = Soup('Борщ', 451);

  borsch.serveMsg();
  print('Острота: ${borsch.scovilles}');
  print('Халяль: ${borsch.isHalal?"да":"нет"}');
  print('Калорий в порции: ${borsch.calculateTotalCalories(borsch.weightGrams)}');

  print('\nТЕСТИРУЕМ ШАУРМУ');
  final shawa = Shawa('Шаурма с курицей', 301);

  shawa.serveMsg();
  print('Халяль: ${shawa.isHalal?"да":"нет"}');
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
  shawa.applySpecialOffer2(sauce:'Острый',discount:15.0);
  //endregion
  //region 6.1
  var list1 = [1,2,3];
  List<String> lols = ["Lol","Kek","Cheburek"];
  print(list1.length);
  print(list1.reversed);
  print(list1.reduce((sum,el)=>sum+=el));
  print(lols.join());
  print(lols[1]);
  print(lols.isEmpty);

  var burger1 = Burger("brgr1",250);
  var burger2 = Burger("brgr2",200);
  var burger3 = Burger("brgr3",450);

  List<Burger> burgers= [burger1,burger2];
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
  for(var i=1;i<1000;i++){
    if (i%2==0) continue;
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
}