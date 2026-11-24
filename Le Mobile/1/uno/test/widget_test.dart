// This is a basic Flutter widget test.
//
// To perform an interaction with a widget in your test, use the WidgetTester
// utility in the flutter_test package. For example, you can send tap and scroll
// gestures. You can also use WidgetTester to find child widgets in the widget
// tree, read text, and verify that the values of widget properties are correct.

import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:uno/main.dart';

void main() {
  test('isolate writes a JSON file and reads the entity back', () async {
    final directory = await Directory.systemTemp.createTemp('uno_isolate_');
    final filePath = '${directory.path}${Platform.pathSeparator}food.json';

    try {
      final result = await writeAndReadJsonFileInIsolate(
        filePath,
        '{"type":"Суп","name":"Тестовый суп","weightGrams":300}',
      );

      expect(result['type'], 'Суп');
      expect(result['name'], 'Тестовый суп');
      expect(await File(filePath).exists(), isTrue);
    } finally {
      await directory.delete(recursive: true);
    }
  });

  testWidgets('menu uses a fixed item extent and opens food details', (
    WidgetTester tester,
  ) async {
    final repository = FakeFoodRepository();
    await tester.pumpWidget(MyApp(repository: repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byTooltip('Сортировка'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Добавить поле'));
    await tester.pumpAndSettle();
    expect(find.byType(DropdownButton<SortField>), findsNWidgets(2));
    await tester.tap(find.byTooltip('По возрастанию').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Применить'));
    await tester.pumpAndSettle();
    expect(repository.lastSorts, hasLength(2));
    expect(repository.lastSorts.first.field, SortField.name);
    expect(repository.lastSorts.last.ascending, isFalse);

    expect(find.text('Меню кухни'), findsOneWidget);
    expect(find.text('Бургер с сыром'), findsOneWidget);

    final listView = tester.widget<ListView>(find.byType(ListView).first);
    expect(listView.itemExtent, 124);

    await tester.tap(find.text('Бургер с сыром'));
    await tester.pumpAndSettle();

    expect(find.text('Информация о блюде'), findsOneWidget);
    expect(find.text('Калорийность на 100 г'), findsOneWidget);
    expect(find.text('Халяль'), findsOneWidget);
    expect(find.text('Острота'), findsOneWidget);
  });

  testWidgets('food can be added, edited, searched, and deleted', (
    WidgetTester tester,
  ) async {
    final repository = FakeFoodRepository();
    await tester.pumpWidget(MyApp(repository: repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byTooltip('Добавить блюдо'));
    await tester.pumpAndSettle();
    await tester.tap(find.byType(DropdownButtonFormField<String>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Суп').last);
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField).at(0), 'Тестовый суп');
    await tester.enterText(find.byType(TextFormField).at(1), '400');
    await tester.enterText(find.byType(TextFormField).at(2), '210');
    await tester.enterText(find.byType(TextFormField).at(3), '75');
    await tester.tap(find.text('Добавить'));
    await tester.pumpAndSettle();

    expect(find.text('Тестовый суп'), findsOneWidget);
    expect(repository.entries, hasLength(4));

    await tester.enterText(find.byType(TextField).first, 'Тестовый');
    await tester.pumpAndSettle();
    expect(find.text('Тестовый суп'), findsOneWidget);
    expect(find.text('Бургер с сыром'), findsNothing);
    await tester.enterText(find.byType(TextField).first, '');
    await tester.pumpAndSettle();

    await tester.tap(find.text('Тестовый суп'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Изменить'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField).at(0), 'Суп обновлён');
    await tester.tap(find.text('Сохранить'));
    await tester.pumpAndSettle();

    expect(find.text('Суп обновлён'), findsOneWidget);
    expect(
      repository.entries.where((entry) => entry.food.name == 'Суп обновлён'),
      hasLength(1),
    );

    await tester.tap(find.text('Суп обновлён'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Удалить'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Удалить').last);
    await tester.pumpAndSettle();

    expect(repository.entries, hasLength(3));
    expect(find.text('Суп обновлён'), findsNothing);
  });

  testWidgets('filesystem page names unsupported platforms', (
    WidgetTester tester,
  ) async {
    debugDefaultTargetPlatformOverride = TargetPlatform.windows;
    try {
      await tester.pumpWidget(MyApp(repository: FakeFoodRepository()));
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('Файловая система'));
      await tester.pumpAndSettle();

      expect(
        find.textContaining('Не поддерживается на Windows.'),
        findsWidgets,
      );
      expect(find.textContaining('Доступно на: Android, iOS.'), findsWidgets);
    } finally {
      debugDefaultTargetPlatformOverride = null;
    }
  });
}

class FakeFoodRepository implements FoodRepository {
  final List<FoodEntry> entries = [
    FoodEntry(
      id: 1,
      food: Burger('Бургер с сыром', 320)
        ..setSpiciness(1200)
        ..isHalal = false,
    ),
    FoodEntry(id: 2, food: Soup('Борщ домашний', 350)..setSpiciness(150)),
    FoodEntry(id: 3, food: Shawa('Шаурма с курицей', 350)),
  ];

  int _nextId = 4;
  String lastQuery = '';
  List<FoodSort> lastSorts = [];

  @override
  Future<List<FoodEntry>> loadFoods({
    String query = '',
    List<FoodSort> sorts = const [],
  }) async {
    lastQuery = query;
    lastSorts = List.of(sorts);
    final normalizedQuery = query.toLowerCase();
    final result = entries.where((entry) {
      return entry.food.name.toLowerCase().contains(normalizedQuery) ||
          _typeOf(entry.food).toLowerCase().contains(normalizedQuery);
    }).toList();
    result.sort((left, right) {
      for (final sort in sorts) {
        final comparison = _compare(left.food, right.food, sort.field);
        if (comparison != 0) return sort.ascending ? comparison : -comparison;
      }
      return left.id.compareTo(right.id);
    });
    return result;
  }

  @override
  Future<int> insertFood(Food food) async {
    final id = _nextId++;
    entries.add(FoodEntry(id: id, food: food));
    return id;
  }

  @override
  Future<void> updateFood(int id, Food food) async {
    final index = entries.indexWhere((entry) => entry.id == id);
    if (index < 0) throw StateError('Entry not found');
    entries[index] = FoodEntry(id: id, food: food);
  }

  @override
  Future<void> deleteFood(int id) async {
    entries.removeWhere((entry) => entry.id == id);
  }

  static String _typeOf(Food food) {
    if (food is Burger) return 'Бургер';
    if (food is Soup) return 'Суп';
    if (food is Shawa) return 'Шаурма';
    return '';
  }

  static int _compare(Food left, Food right, SortField field) {
    switch (field) {
      case SortField.name:
        return left.name.toLowerCase().compareTo(right.name.toLowerCase());
      case SortField.type:
        return _typeOf(left).compareTo(_typeOf(right));
      case SortField.weight:
        return left.weightGrams.compareTo(right.weightGrams);
      case SortField.calories:
        return _calories(left).compareTo(_calories(right));
      case SortField.halal:
        return _halal(left).toString().compareTo(_halal(right).toString());
      case SortField.spiciness:
        return _spiciness(left).compareTo(_spiciness(right));
    }
  }

  static int _calories(Food food) {
    if (food case final Calories calories) return calories.calories100g.toInt();
    return 0;
  }

  static bool _halal(Food food) {
    if (food case final Halal halal) return halal.isHalal;
    return false;
  }

  static int _spiciness(Food food) {
    if (food case final SpicyMixin spicy) return spicy.scovilleUnits;
    return 0;
  }
}
