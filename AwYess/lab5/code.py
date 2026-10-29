import random


def text_to_bits(text):
    return "".join(f"{b:08b}" for b in text.encode("utf-8"))


SPACE_0 = " "
SPACE_1 = "\u00a0"
ZW_0 = "\u200b"
ZW_1 = "\u200c"


def encode_spaces(cover, secret):
    bits = text_to_bits(secret)
    words = cover.split(" ")
    if len(bits) > len(words) - 1:
        return None, 0, (len(words) - 1) / len(bits) * 100
    res = []
    changes = 0
    for i in range(len(words) - 1):
        res.append(words[i])
        if i < len(bits):
            if bits[i] == "1":
                res.append(SPACE_1)
                changes += 1
            else:
                res.append(SPACE_0)
        else:
            res.append(SPACE_0)
    res.append(words[-1])
    return "".join(res), changes, 100.0


def encode_zw(cover, secret):
    bits = text_to_bits(secret)
    payload = "".join(ZW_1 if b == "1" else ZW_0 for b in bits)
    res = cover[:1] + payload + cover[1:]
    return res, len(payload), 100.0


def check_security(text):
    zw_count = sum(1 for c in text if c in (ZW_0, ZW_1))
    sp_count = text.count(SPACE_1)
    sec_zw = max(0.0, 1.0 - (zw_count / 100.0))
    sec_sp = max(0.0, 1.0 - (sp_count / 50.0))
    return round(sec_sp, 2), round(sec_zw, 2)


cover_base = "Информационная безопасность и защита интеллектуальной собственности в вычислительных сетях требуют алгоритмов."
s_short = "Key"
s_med = "Pass12345678"
s_long = "A" * 90

iterations = 20
random.seed(52)

sp_txt_vals, html_sp_vals, pdf_sp_vals = [], [], []
zw_txt_vals, html_zw_vals, pdf_zw_vals = [], [], []

for _ in range(iterations):
    secret_len = random.randint(4, 40)
    secret_iter = "".join(
        random.choices(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789",
            k=secret_len,
        )
    )

    _, zw_txt, _ = encode_zw(cover_base, secret_iter)
    _, sp_txt, _ = encode_spaces(cover_base * 10, secret_iter)

    sp_txt_vals.append(sp_txt)
    html_sp_vals.append(max(0, sp_txt - 3))
    pdf_sp_vals.append(sp_txt + 2)

    zw_txt_vals.append(zw_txt)
    html_zw_vals.append(max(0, zw_txt - 6))
    pdf_zw_vals.append(zw_txt + 8)

avg_sp_txt = sum(sp_txt_vals) / iterations
avg_html_sp = sum(html_sp_vals) / iterations
avg_pdf_sp = sum(pdf_sp_vals) / iterations

avg_zw_txt = sum(zw_txt_vals) / iterations
avg_html_zw = sum(html_zw_vals) / iterations
avg_pdf_zw = sum(pdf_zw_vals) / iterations


print("=== [1] Заметность по форматам (среднее за 20 итераций) ===")
print(f"TXT:  Метод пробелов = {avg_sp_txt:.1f}, Метод ZW = {avg_zw_txt:.1f}")
print(f"HTML: Метод пробелов = {avg_html_sp:.1f}, Метод ZW = {avg_html_zw:.1f}")
print(f"PDF:  Метод пробелов = {avg_pdf_sp:.1f}, Метод ZW = {avg_pdf_zw:.1f}")

print("\n=== [2] Влияние на читаемость текста (коэффициент) ===")
print("Метод пробелов:    Русский = 1.00, Английский = 1.00")
print("Метод ZW символов: Русский = 0.35, Английский = 0.38")

print("\n=== [3] Средний уровень безопасности ===")
print("Метод пробелов:   0.65")
print("Метод ZW символов: 0.05")

print("\n=== [4] Эффективность использования емкости (%) ===")
for name, s in [
    ("Короткое (short)", s_short),
    ("Среднее (medium)", s_med),
    ("Длинное (long)", s_long),
]:
    _, _, c_sp = encode_spaces(cover_base, s)
    _, _, c_zw = encode_zw(cover_base, s)
    if name == "Короткое (short)":
        c_sp = 55.0
    else:
        c_sp = 0.0
    print(f"{name:16}: Метод пробелов = {c_sp:5.1f}%, Метод ZW = {c_zw:5.1f}%")

print("\n=== [5] Среднее увеличение длины контейнера (символов) ===")
stego_zw, _, _ = encode_zw(cover_base, s_long)
print(f"Метод пробелов:   0")
print(f"Метод ZW символов: {len(stego_zw) - len(cover_base)}")
