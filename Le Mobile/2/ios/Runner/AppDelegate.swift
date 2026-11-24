import Flutter
import UIKit

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
  private var pendingCameraResult: FlutterResult?

  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }

  func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
    GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)

    let registrar = engineBridge.pluginRegistry.registrar(forPlugin: "DeviceMethodChannels")
    let messenger = registrar.messenger()

    FlutterMethodChannel(name: "device_actions", binaryMessenger: messenger)
      .setMethodCallHandler { [weak self] call, result in
        guard let self else {
          result(FlutterError(
            code: "APP_UNAVAILABLE",
            message: "Обработчик приложения недоступен",
            details: nil
          ))
          return
        }

        switch call.method {
        case "openMap":
          self.openMap(arguments: call.arguments, result: result)
        case "capturePhoto":
          self.capturePhoto(result: result)
        default:
          result(FlutterMethodNotImplemented)
        }
      }

    FlutterMethodChannel(name: "battery_channel", binaryMessenger: messenger)
      .setMethodCallHandler { call, result in
        guard call.method == "getBatteryLevel" else {
          result(FlutterMethodNotImplemented)
          return
        }

        UIDevice.current.isBatteryMonitoringEnabled = true
        let batteryLevel = UIDevice.current.batteryLevel
        guard batteryLevel >= 0 else {
          result(FlutterError(
            code: "BATTERY_UNAVAILABLE",
            message: "Уровень заряда батареи недоступен",
            details: nil
          ))
          return
        }

        result(Int((batteryLevel * 100).rounded()))
      }
  }

  private func openMap(arguments: Any?, result: @escaping FlutterResult) {
    guard
      let coordinates = arguments as? [String: Any],
      let latitude = coordinates["latitude"] as? Double,
      let longitude = coordinates["longitude"] as? Double,
      latitude.isFinite,
      longitude.isFinite,
      (-90.0...90.0).contains(latitude),
      (-180.0...180.0).contains(longitude)
    else {
      result(FlutterError(
        code: "INVALID_COORDINATES",
        message: "Укажите корректные координаты",
        details: nil
      ))
      return
    }

    var components = URLComponents()
    components.scheme = "https"
    components.host = "maps.apple.com"
    components.queryItems = [
      URLQueryItem(name: "ll", value: "\(latitude),\(longitude)")
    ]

    guard let url = components.url else {
      result(FlutterError(
        code: "MAP_URL_INVALID",
        message: "Не удалось сформировать ссылку на карту",
        details: nil
      ))
      return
    }

    UIApplication.shared.open(url, options: [:]) { opened in
      if opened {
        result(nil)
      } else {
        result(FlutterError(
          code: "MAP_LAUNCH_FAILED",
          message: "Не удалось открыть приложение карт",
          details: nil
        ))
      }
    }
  }

  private func capturePhoto(result: @escaping FlutterResult) {
    guard UIImagePickerController.isSourceTypeAvailable(.camera) else {
      result(FlutterError(
        code: "CAMERA_UNAVAILABLE",
        message: "Камера недоступна на этом устройстве",
        details: nil
      ))
      return
    }

    guard pendingCameraResult == nil else {
      result(FlutterError(
        code: "CAMERA_REQUEST_IN_PROGRESS",
        message: "Съёмка фотографии уже выполняется",
        details: nil
      ))
      return
    }

    guard let presenter = topViewController() else {
      result(FlutterError(
        code: "CAMERA_PRESENTATION_FAILED",
        message: "Не удалось открыть камеру",
        details: nil
      ))
      return
    }

    let picker = UIImagePickerController()
    picker.sourceType = .camera
    picker.delegate = self
    pendingCameraResult = result
    presenter.present(picker, animated: true)
  }

  private func topViewController() -> UIViewController? {
    let windows = UIApplication.shared.connectedScenes
      .compactMap { $0 as? UIWindowScene }
      .flatMap { $0.windows }
    var controller = windows.first(where: \.isKeyWindow)?.rootViewController

    while let presented = controller?.presentedViewController {
      controller = presented
    }

    return controller
  }
}

extension AppDelegate: UIImagePickerControllerDelegate, UINavigationControllerDelegate {
  func imagePickerController(
    _ picker: UIImagePickerController,
    didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]
  ) {
    let imageData = (info[.originalImage] as? UIImage)?.jpegData(compressionQuality: 0.9)
    picker.dismiss(animated: true) { [weak self] in
      guard let self else { return }
      let result = self.pendingCameraResult
      self.pendingCameraResult = nil

      guard let imageData else {
        result?(FlutterError(
          code: "PHOTO_ENCODING_FAILED",
          message: "Не удалось подготовить фотографию",
          details: nil
        ))
        return
      }

      result?(FlutterStandardTypedData(bytes: imageData))
    }
  }

  func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
    picker.dismiss(animated: true) { [weak self] in
      guard let self else { return }
      let result = self.pendingCameraResult
      self.pendingCameraResult = nil
      result?(nil)
    }
  }
}
