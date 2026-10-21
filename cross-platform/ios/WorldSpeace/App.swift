import UIKit
import WebKit
import UniformTypeIdentifiers
import AVFoundation

@main
final class AppDelegate: UIResponder, UIApplicationDelegate {
    var window: UIWindow?
    func application(_ application: UIApplication, didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        let window = UIWindow(frame: UIScreen.main.bounds)
        window.rootViewController = LearningViewController()
        window.makeKeyAndVisible()
        self.window = window
        return true
    }
}

final class Assets: NSObject, WKURLSchemeHandler {
    var picked: Data?
    func webView(_ webView: WKWebView, start urlSchemeTask: WKURLSchemeTask) {
        guard let url = urlSchemeTask.request.url, url.scheme == "worldspeace", url.host == "app", urlSchemeTask.request.httpMethod == "GET" else {
            urlSchemeTask.didFailWithError(URLError(.unsupportedURL)); return
        }
        let name = url.path
        var bytes: Data?
        if name == "/import.txt" { bytes = picked }
        else if name.hasPrefix("/assets/"), !name.contains("\\"), !name.split(separator: "/").contains(".."), let root = Bundle.main.resourceURL?.appendingPathComponent("www", isDirectory: true) {
            let relative = String(name.dropFirst("/assets/".count))
            let file = root.appendingPathComponent(relative).standardizedFileURL
            if file.path.hasPrefix(root.path + "/") { bytes = try? Data(contentsOf: file) }
        }
        let mime = ["html":"text/html", "mjs":"text/javascript", "css":"text/css", "png":"image/png", "json":"application/json", "tsv":"text/plain", "txt":"text/plain"][url.pathExtension] ?? "application/octet-stream"
        let status = bytes == nil ? 404 : 200
        let response = HTTPURLResponse(url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: ["Content-Type": mime + (mime == "image/png" ? "" : "; charset=utf-8"), "Cache-Control":"no-store", "Access-Control-Allow-Origin":"worldspeace://app"])!
        urlSchemeTask.didReceive(response)
        urlSchemeTask.didReceive(bytes ?? Data())
        urlSchemeTask.didFinish()
    }
    func webView(_ webView: WKWebView, stop urlSchemeTask: WKURLSchemeTask) {}
}

final class LearningViewController: UIViewController, WKUIDelegate, WKNavigationDelegate, UIDocumentPickerDelegate, WKScriptMessageHandler {
    private let assets = Assets()
    private let speaker = AVSpeechSynthesizer()
    private let limit = 20 * 1024 * 1024
    private var browser: WKWebView!
    private var pendingOperation = ""
    private var lastExport = ""
    private var smokePhase = 0
    private var smokeSaved = ""
    private var smokeMode: Bool {
        #if DEBUG
        return ProcessInfo.processInfo.arguments.contains("--smoke-test")
        #else
        return false
        #endif
    }
    private var dataRoot: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        return base.appendingPathComponent(smokeMode ? "world-speace-smoke" : "world-speace", isDirectory: true)
    }
    private var progress: URL { dataRoot.appendingPathComponent("progress.json") }
    private var previous: URL { dataRoot.appendingPathComponent("before-restore.json") }
    private func read(_ url: URL) throws -> String {
        if !FileManager.default.fileExists(atPath: url.path) { return "" }
        let attributes = try FileManager.default.attributesOfItem(atPath: url.path)
        guard (attributes[.size] as? NSNumber)?.intValue ?? 0 <= limit else { throw failure("文件超过 20 MB，请保留原始进度。") }
        let bytes = try Data(contentsOf: url)
        guard let text = String(data: bytes, encoding: .utf8) else { throw failure("进度不是有效的 UTF-8 文件。") }
        return text
    }
    private func write(_ text: String, to url: URL) throws {
        guard let bytes = text.data(using: .utf8), bytes.count <= limit else { throw failure("文件不能超过 20 MB。") }
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try bytes.write(to: url, options: .atomic)
        var destination = url
        var attributes = URLResourceValues(); attributes.isExcludedFromBackup = true
        try? destination.setResourceValues(attributes)
    }
    private func failure(_ message: String) -> NSError { NSError(domain: "world-speace", code: 1, userInfo: [NSLocalizedDescriptionKey: message]) }
    private func json(_ value: Any) -> String { String(data: (try? JSONSerialization.data(withJSONObject: value, options: [.fragmentsAllowed])) ?? Data("null".utf8), encoding: .utf8)! }
    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = UIColor(red: 241/255, green: 246/255, blue: 242/255, alpha: 1)
        let configuration = WKWebViewConfiguration()
        configuration.setURLSchemeHandler(assets, forURLScheme: "worldspeace")
        let bridge = try! String(contentsOf: Bundle.main.url(forResource: "bridge", withExtension: "js")!, encoding: .utf8)
        configuration.userContentController.addUserScript(WKUserScript(source: bridge, injectionTime: .atDocumentStart, forMainFrameOnly: true))
        configuration.userContentController.add(self, name: "worldSpeaceStatus")
        configuration.preferences.javaScriptCanOpenWindowsAutomatically = false
        browser = WKWebView(frame: .zero, configuration: configuration)
        browser.uiDelegate = self; browser.navigationDelegate = self
        browser.scrollView.contentInsetAdjustmentBehavior = .never
        browser.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(browser)
        NSLayoutConstraint.activate([browser.topAnchor.constraint(equalTo: view.safeAreaLayoutGuide.topAnchor), browser.bottomAnchor.constraint(equalTo: view.bottomAnchor), browser.leadingAnchor.constraint(equalTo: view.leadingAnchor), browser.trailingAnchor.constraint(equalTo: view.trailingAnchor)])
        browser.load(URLRequest(url: URL(string: "worldspeace://app/assets/index.html")!))
        NotificationCenter.default.addObserver(self, selector: #selector(stopSpeaking), name: UIApplication.willResignActiveNotification, object: nil)
    }
    @objc private func stopSpeaking() { speaker.stopSpeaking(at: .immediate) }
    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        let url = navigationAction.request.url
        decisionHandler(url?.scheme == "worldspeace" && url?.host == "app" && navigationAction.targetFrame?.isMainFrame == true ? .allow : .cancel)
    }
    func webView(_ webView: WKWebView, runJavaScriptTextInputPanelWithPrompt prompt: String, defaultText: String?, initiatedByFrame frame: WKFrameInfo, completionHandler: @escaping (String?) -> Void) {
        let prefix = "__WORLD_SPEACE__"
        guard frame.isMainFrame, frame.request.url?.scheme == "worldspeace", frame.request.url?.host == "app", prompt.hasPrefix(prefix), let payload = String(prompt.dropFirst(prefix.count)).data(using: .utf8), let message = try? JSONSerialization.jsonObject(with: payload) as? [String:Any], let method = message["method"] as? String else { completionHandler(nil); return }
        let args = message["args"] as? [String] ?? []
        do { completionHandler(try call(method, args)) }
        catch { completionHandler(method == "readState" ? json(["ok":false,"error":"无法读取学习进度："+error.localizedDescription]) : "操作未完成："+error.localizedDescription) }
    }
    private func call(_ method: String, _ args: [String]) throws -> String {
        switch method {
        case "readState": return json(["ok":true,"state":try read(progress)])
        case "rawState": return try read(progress)
        case "previousBackup": return try read(previous)
        case "saveState":
            guard let text = args.first, let bytes = text.data(using: .utf8), bytes.count <= limit, let state = try JSONSerialization.jsonObject(with: bytes) as? [String:Any], state["Version"] as? Int == 1, state["Cards"] is [Any] || state["Cards"] is [String:Any] else { throw failure("学习进度格式无效。") }
            try write(text, to: progress); return ""
        case "preserveBeforeRestore":
            let original = try read(progress); if !original.isEmpty { try write(original, to: previous) }; return ""
        case "voices": return json(AVSpeechSynthesisVoice.speechVoices().filter { $0.language.hasPrefix("en") }.map { $0.identifier })
        case "speak":
            let english = AVSpeechSynthesisVoice.speechVoices().filter { $0.language.hasPrefix("en") }
            guard let voice = english.first(where: { $0.identifier == args.last }) ?? english.first else { return "未找到离线英语语音，请在系统设置中安装。" }
            speaker.stopSpeaking(at: .immediate)
            let phrase = AVSpeechUtterance(string: String((args.first ?? "").prefix(1000))); phrase.voice = voice; phrase.rate = AVSpeechUtteranceDefaultSpeechRate * 0.85; speaker.speak(phrase); return ""
        case "stopSpeech": stopSpeaking(); return ""
        case "releasePicked": assets.picked = nil; return ""
        case "pickDocument":
            guard let operation = args.first, ["backup","book"].contains(operation) else { throw failure("文件操作无效。") }
            DispatchQueue.main.async { self.pick(operation) }; return ""
        case "exportDocument":
            guard args.count == 3 else { throw failure("导出参数无效。") }
            if smokeMode { lastExport = args[2]; emit(["kind":"exported"]); return "" }
            let name = URL(fileURLWithPath: args[0]).lastPathComponent
            let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString, isDirectory: true)
            let target = directory.appendingPathComponent(name)
            try write(args[2], to: target)
            DispatchQueue.main.async { self.export(target) }; return ""
        default: throw failure("未知的本机操作。")
        }
    }
    private func pick(_ operation: String) {
        guard presentedViewController == nil else { return }
        pendingOperation = operation
        let picker = UIDocumentPickerViewController(forOpeningContentTypes: operation == "backup" ? [.json] : [.commaSeparatedText, .tabSeparatedText, .plainText], asCopy: true)
        picker.delegate = self; picker.allowsMultipleSelection = false; present(picker, animated: true)
    }
    private func export(_ url: URL) {
        guard presentedViewController == nil else { return }
        pendingOperation = "export"
        let picker = UIDocumentPickerViewController(forExporting: [url], asCopy: true)
        picker.delegate = self; present(picker, animated: true)
    }
    func documentPicker(_ controller: UIDocumentPickerViewController, didPickDocumentsAt urls: [URL]) {
        guard let url = urls.first else { return }
        if pendingOperation == "export" { emit(["kind":"exported"]); return }
        let accessed = url.startAccessingSecurityScopedResource(); defer { if accessed { url.stopAccessingSecurityScopedResource() } }
        do {
            let text = try read(url); assets.picked = Data(text.utf8)
            emit(["kind":"picked","operation":pendingOperation,"name":url.lastPathComponent])
        } catch {
            controller.dismiss(animated: true) { let alert = UIAlertController(title: "无法读取文件", message: error.localizedDescription, preferredStyle: .alert); alert.addAction(UIAlertAction(title: "知道了", style: .default)); self.present(alert, animated: true) }
        }
    }
    private func emit(_ detail: [String:String]) {
        browser.evaluateJavaScript("window.dispatchEvent(new CustomEvent('native-result',{detail:" + json(detail) + "}))", completionHandler: nil)
    }
    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        #if DEBUG
        if smokeMode, message.frameInfo.isMainFrame, message.body as? String == "ready" { runSmoke() }
        #endif
    }
    #if DEBUG
    private func smokeResult(_ passed: Bool, _ message: String) {
        let result = json(["passed":passed,"message":message,"platform":"iOS Simulator","isolatedTestData":true] as [String:Any])
        let output = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0].appendingPathComponent("smoke-result.json")
        try? write(result, to: output)
    }
    private func runSmoke() {
        if smokePhase == 0 {
            smokePhase = 1
            let script = """
            (()=>{const click=s=>{const el=document.querySelector(s);if(!el)throw new Error('Missing '+s);el.click();};click('[data-action="start-new"]');click('[data-action="reveal"]');click('[data-grade="2"]');click('[data-action="today"]');click('[data-action="settings"]');const target=document.getElementById('DailyTarget');target.value='30';target.dispatchEvent(new Event('change',{bubbles:true}));click('[data-action="export-backup"]');return document.documentElement.dataset.ready==='true';})()
            """
            browser.evaluateJavaScript(script) { _, error in
                do {
                    if let error = error { throw error }
                    let text = try self.read(self.progress)
                    let state = try JSONSerialization.jsonObject(with: Data(text.utf8)) as! [String:Any]
                    guard (state["Cards"] as? [Any])?.count == 1, (state["History"] as? [Any])?.count == 1, state["DailyTarget"] as? Int == 30, self.lastExport == text else { throw self.failure("Native persistence or backup differs") }
                    self.smokeSaved = text; self.browser.reload()
                } catch { self.smokeResult(false, error.localizedDescription) }
            }
        } else if smokePhase == 1 {
            smokePhase = 2
            do { guard try read(progress) == smokeSaved else { throw failure("Progress changed on reopen") }; smokeResult(true, "Packaged dictionary, study, atomic native save, JSON backup and reopen passed") }
            catch { smokeResult(false, error.localizedDescription) }
        }
    }
    #endif
}
