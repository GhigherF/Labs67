import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:design/main.dart';

void main() {
  testWidgets('device tools show battery and validate map coordinates', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1200, 2400);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    const batteryChannel = MethodChannel('battery_channel');
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(batteryChannel, (call) async => 87);
    addTearDown(
      () => TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(batteryChannel, null),
    );

    await tester.pumpWidget(const MyApp());
    await tester.tap(find.byTooltip('Инструменты устройства'));
    await tester.pumpAndSettle();

    expect(find.text('Открыть карту'), findsOneWidget);
    expect(find.text('87%'), findsOneWidget);

    await tester.enterText(find.widgetWithText(TextField, 'Широта'), '91');
    await tester.tap(find.text('Открыть в приложении карт'));
    await tester.pump();

    expect(
      find.text('Введите корректные координаты широты и долготы'),
      findsOneWidget,
    );
  });
}
