// batterymonitor_rs.rs — монитор батареи с графиками на Rust (консоль + termion)

use rand::Rng;
use std::io::{self, Write, BufRead};
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use std::thread;
use termion::{color, style, cursor, clear};

struct BatteryMonitor {
    charge_history: Vec<f64>,
    temp_history: Vec<f64>,
    time_history: Vec<u64>,
    current_charge: f64,
    current_temp: f64,
    remaining_time: i64,
    notified_low: bool,
    notified_high: bool,
    history_size: usize,
}

impl BatteryMonitor {
    fn new() -> Self {
        let mut b = BatteryMonitor {
            charge_history: Vec::new(),
            temp_history: Vec::new(),
            time_history: Vec::new(),
            current_charge: 100.0,
            current_temp: 25.0,
            remaining_time: 999,
            notified_low: false,
            notified_high: false,
            history_size: 60,
        };
        // Начальные данные
        for _ in 0..10 {
            b.charge_history.push(b.current_charge);
            b.temp_history.push(b.current_temp);
            b.time_history.push(Self::current_time());
        }
        b
    }

    fn current_time() -> u64 {
        SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_secs()
    }

    fn update(&mut self) {
        let mut rng = rand::thread_rng();
        let change = rng.gen_range(-0.5..0.6);
        self.current_charge += change;
        self.current_charge = self.current_charge.max(0.0).min(100.0);
        let temp_change = rng.gen_range(-2.0..7.0) + (100.0 - self.current_charge) * 0.1;
        self.current_temp += temp_change;
        self.current_temp = self.current_temp.max(20.0).min(60.0);

        // Время
        if self.charge_history.len() > 5 {
            let rate = (self.charge_history.last().unwrap() - self.charge_history[0]) / self.charge_history.len() as f64;
            if rate < 0.0 {
                self.remaining_time = (self.current_charge / rate.abs() * 60.0) as i64;
            } else {
                self.remaining_time = 999;
            }
        } else {
            self.remaining_time = 999;
        }

        self.charge_history.push(self.current_charge);
        self.temp_history.push(self.current_temp);
        self.time_history.push(Self::current_time());
        if self.charge_history.len() > self.history_size {
            self.charge_history.remove(0);
            self.temp_history.remove(0);
            self.time_history.remove(0);
        }

        // Уведомления
        if self.current_charge < 20.0 && !self.notified_low {
            self.notified_low = true;
            println!("\n🔋 Низкий заряд: {:.1}%! Подключите зарядку.", self.current_charge);
        } else if self.current_charge >= 25.0 {
            self.notified_low = false;
        }
        if self.current_temp > 50.0 && !self.notified_high {
            self.notified_high = true;
            println!("\n🌡️ Высокая температура: {:.1}°C! Охладите устройство.", self.current_temp);
        } else if self.current_temp <= 45.0 {
            self.notified_high = false;
        }
    }

    fn display(&self) {
        print!("{}{}", clear::All, cursor::Goto(1, 1));
        println!("🔋 BatteryMonitor Pro — Rust Edition");
        println!("Заряд: {:.1}%  ", self.current_charge);
        if self.remaining_time < 999 {
            println!("Время: {}ч {}мин  ", self.remaining_time/60, self.remaining_time%60);
        } else {
            println!("Время: ∞  ");
        }
        println!("Температура: {:.1}°C", self.current_temp);

        // Статус
        let (status, color) = if self.current_charge > 80.0 {
            ("Отлично", color::Fg(color::Green))
        } else if self.current_charge > 50.0 {
            ("Хорошо", color::Fg(color::Blue))
        } else if self.current_charge > 20.0 {
            ("Нормально", color::Fg(color::Yellow))
        } else {
            ("Критично!", color::Fg(color::Red))
        };
        println!("Статус: {}{}{}", color, status, style::Reset);

        // ASCII график
        println!("\nГрафик заряда за последние {} сек:", self.history_size);
        if self.charge_history.len() > 1 {
            const WIDTH: usize = 50;
            const HEIGHT: usize = 10;
            let max_val = 105.0;
            let min_val = 0.0;
            let data = &self.charge_history;
            let step = (data.len() - 1) as f64 / (WIDTH - 1) as f64;
            let mut grid = vec![vec![' '; WIDTH]; HEIGHT];
            for i in 0..WIDTH {
                let idx = (i as f64 * step) as usize;
                let idx = if idx >= data.len() { data.len() - 1 } else { idx };
                let val = data[idx];
                let y = ((val - min_val) / (max_val - min_val) * (HEIGHT - 1) as f64) as usize;
                let y = if y >= HEIGHT { HEIGHT - 1 } else { y };
                grid[HEIGHT - 1 - y][i] = '█';
            }
            for i in 0..HEIGHT {
                print!("{:3}% ", (HEIGHT-1-i)*100/(HEIGHT-1));
                for j in 0..WIDTH {
                    if grid[i][j] == '█' {
                        print!("{}█{}", color::Fg(color::Blue), style::Reset);
                    } else {
                        print!(" ");
                    }
                }
                println!();
            }
        }
        println!("\nКоманды: refresh, export, reset, exit");
        print!("> ");
        io::stdout().flush().unwrap();
    }

    fn export_csv(&self) {
        if self.charge_history.len() < 2 {
            println!("Недостаточно данных для экспорта");
            return;
        }
        let filename = format!("battery_data_{}.csv", Self::current_time());
        if let Ok(mut file) = std::fs::File::create(&filename) {
            use std::io::Write;
            writeln!(file, "Время,Заряд(%),Температура(°C)").unwrap();
            for i in 0..self.charge_history.len() {
                writeln!(file, "{},{:.2},{:.2}", self.time_history[i], self.charge_history[i], self.temp_history[i]).unwrap();
            }
            println!("Данные сохранены в {}", filename);
        } else {
            println!("Ошибка создания файла");
        }
    }

    fn reset(&mut self) {
        self.charge_history.clear();
        self.temp_history.clear();
        self.time_history.clear();
        self.current_charge = 100.0;
        self.current_temp = 25.0;
        self.remaining_time = 999;
        for _ in 0..10 {
            self.charge_history.push(self.current_charge);
            self.temp_history.push(self.current_temp);
            self.time_history.push(Self::current_time());
        }
        println!("Данные сброшены");
    }
}

fn main() {
    let mut monitor = BatteryMonitor::new();
    let stdin = io::stdin();
    let mut reader = stdin.lock();
    println!("🔋 BatteryMonitor Pro — Rust Edition");
    println!("Нажмите Enter для обновления, или введите команду: export, reset, exit");
    loop {
        monitor.update();
        monitor.display();
        let mut input = String::new();
        if reader.read_line(&mut input).is_err() { break; }
        let input = input.trim();
        match input {
            "export" => monitor.export_csv(),
            "reset" => monitor.reset(),
            "exit" => {
                println!("До свидания!");
                break;
            }
            _ => {}
        }
        thread::sleep(Duration::from_millis(100));
    }
}
