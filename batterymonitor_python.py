# batterymonitor_python.py — монитор батареи с графиками на Python (Tkinter + Matplotlib)

import tkinter as tk
from tkinter import ttk, messagebox
import matplotlib.pyplot as plt
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
import numpy as np
import threading
import time
import json
import os
from collections import deque
import random

class BatteryMonitor:
    def __init__(self, root):
        self.root = root
        self.root.title("🔋 BatteryMonitor Pro — Python")
        self.root.geometry("800x600")
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

        # Данные
        self.history = deque(maxlen=60)  # последние 60 значений заряда (%)
        self.temp_history = deque(maxlen=60)
        self.time_history = deque(maxlen=60)
        self.current_charge = 100.0
        self.current_temp = 25.0
        self.remaining_time = 0  # минут
        self.running = True
        self.sim_speed = 1.0

        # Настройки
        self.config_file = "battery_config.json"
        self.load_config()

        # GUI
        self.create_widgets()
        self.update_loop()
        self.start_simulation()

    def create_widgets(self):
        # Верхняя панель с информацией
        top_frame = tk.Frame(self.root)
        top_frame.pack(fill=tk.X, padx=10, pady=5)

        self.charge_label = tk.Label(top_frame, text="Заряд: 100%", font=("Arial", 16))
        self.charge_label.pack(side=tk.LEFT, padx=10)

        self.time_label = tk.Label(top_frame, text="Время: --", font=("Arial", 16))
        self.time_label.pack(side=tk.LEFT, padx=10)

        self.temp_label = tk.Label(top_frame, text="Температура: 25°C", font=("Arial", 16))
        self.temp_label.pack(side=tk.LEFT, padx=10)

        self.status_label = tk.Label(top_frame, text="Статус: Норма", font=("Arial", 16), fg="green")
        self.status_label.pack(side=tk.LEFT, padx=10)

        # График
        fig, self.ax = plt.subplots(figsize=(6, 3), dpi=100)
        self.ax.set_ylim(0, 105)
        self.ax.set_xlabel("Время (сек)")
        self.ax.set_ylabel("Заряд (%)")
        self.ax.grid(True)
        self.line, = self.ax.plot([], [], 'b-', linewidth=2)
        self.ax_temp = self.ax.twinx()
        self.ax_temp.set_ylabel("Температура (°C)")
        self.temp_line, = self.ax_temp.plot([], [], 'r-', linewidth=1, alpha=0.7)

        self.canvas = FigureCanvasTkAgg(fig, master=self.root)
        self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True, padx=10, pady=5)

        # Кнопки управления
        btn_frame = tk.Frame(self.root)
        btn_frame.pack(pady=5)
        tk.Button(btn_frame, text="Экспорт CSV", command=self.export_csv).pack(side=tk.LEFT, padx=5)
        tk.Button(btn_frame, text="Сбросить данные", command=self.reset_data).pack(side=tk.LEFT, padx=5)

        # Статус
        self.status = tk.Label(self.root, text="Готов", anchor=tk.W)
        self.status.pack(fill=tk.X, padx=10)

    def start_simulation(self):
        # Запуск симуляции данных в отдельном потоке
        def simulate():
            while self.running:
                # Симуляция разряда и случайных колебаний
                change = random.uniform(-0.5, 0.1)  # в основном разряд
                self.current_charge = max(0, min(100, self.current_charge + change))
                self.current_temp = 25 + random.uniform(-2, 5) + (100 - self.current_charge) * 0.1
                self.current_temp = max(20, min(60, self.current_temp))

                # Расчёт оставшегося времени (грубо)
                if len(self.history) > 5:
                    rate = (self.history[-1] - self.history[0]) / len(self.history)
                    if rate < 0:
                        self.remaining_time = int(self.current_charge / abs(rate) * 60)  # минут
                    else:
                        self.remaining_time = 999
                else:
                    self.remaining_time = 999

                # Сохраняем в историю
                self.history.append(self.current_charge)
                self.temp_history.append(self.current_temp)
                self.time_history.append(time.time())

                # Обновление GUI
                self.root.after(0, self.update_gui)

                # Проверка уведомлений
                if self.current_charge < 20 and not self.notified_low:
                    self.notified_low = True
                    messagebox.showwarning("Низкий заряд", f"Уровень батареи {self.current_charge:.1f}%! Подключите зарядку.")
                elif self.current_charge >= 25:
                    self.notified_low = False

                if self.current_temp > 50 and not self.notified_high_temp:
                    self.notified_high_temp = True
                    messagebox.showwarning("Высокая температура", f"Температура батареи {self.current_temp:.1f}°C! Охладите устройство.")
                elif self.current_temp <= 45:
                    self.notified_high_temp = False

                time.sleep(1)

        self.notified_low = False
        self.notified_high_temp = False
        threading.Thread(target=simulate, daemon=True).start()

    def update_gui(self):
        # Обновление текстовых меток
        self.charge_label.config(text=f"Заряд: {self.current_charge:.1f}%")
        if self.remaining_time < 999:
            hours = self.remaining_time // 60
            minutes = self.remaining_time % 60
            self.time_label.config(text=f"Время: {hours}ч {minutes}мин")
        else:
            self.time_label.config(text="Время: ∞")
        self.temp_label.config(text=f"Температура: {self.current_temp:.1f}°C")

        # Статус
        if self.current_charge > 80:
            self.status_label.config(text="Статус: Отлично", fg="green")
        elif self.current_charge > 50:
            self.status_label.config(text="Статус: Хорошо", fg="blue")
        elif self.current_charge > 20:
            self.status_label.config(text="Статус: Нормально", fg="orange")
        else:
            self.status_label.config(text="Статус: Критично!", fg="red")

        # График
        if len(self.history) > 1:
            x_data = list(range(len(self.history)))
            self.line.set_data(x_data, list(self.history))
            self.temp_line.set_data(x_data, list(self.temp_history))
            self.ax.relim()
            self.ax.autoscale_view()
            self.ax_temp.relim()
            self.ax_temp.autoscale_view()
            self.canvas.draw_idle()

    def update_loop(self):
        if self.running:
            self.root.after(100, self.update_loop)

    def export_csv(self):
        if len(self.history) < 2:
            messagebox.showinfo("Информация", "Недостаточно данных для экспорта")
            return
        filename = f"battery_data_{int(time.time())}.csv"
        with open(filename, 'w') as f:
            f.write("Время,Заряд(%),Температура(°C)\n")
            for t, charge, temp in zip(self.time_history, self.history, self.temp_history):
                f.write(f"{t},{charge:.2f},{temp:.2f}\n")
        self.status.config(text=f"Экспортировано в {filename}")

    def reset_data(self):
        self.history.clear()
        self.temp_history.clear()
        self.time_history.clear()
        self.current_charge = 100
        self.current_temp = 25
        self.remaining_time = 0
        self.status.config(text="Данные сброшены")

    def load_config(self):
        if os.path.exists(self.config_file):
            with open(self.config_file, 'r') as f:
                data = json.load(f)
                self.sim_speed = data.get('sim_speed', 1.0)

    def save_config(self):
        data = {'sim_speed': self.sim_speed}
        with open(self.config_file, 'w') as f:
            json.dump(data, f)

    def on_close(self):
        self.running = False
        self.save_config()
        self.root.destroy()

if __name__ == "__main__":
    root = tk.Tk()
    app = BatteryMonitor(root)
    root.mainloop()
