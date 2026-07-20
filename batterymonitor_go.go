// batterymonitor_go.go — монитор батареи с графиками на Go (консоль + ASCII)

package main

import (
	"bufio"
	"fmt"
	"math"
	"math/rand"
	"os"
	"strconv"
	"strings"
	"time"
)

type BatteryMonitor struct {
	chargeHistory  []float64
	tempHistory    []float64
	timeHistory    []int64
	currentCharge  float64
	currentTemp    float64
	remainingTime  int
	notifiedLow    bool
	notifiedHigh   bool
	historySize    int
}

func NewBatteryMonitor() *BatteryMonitor {
	b := &BatteryMonitor{
		chargeHistory: make([]float64, 0, 60),
		tempHistory:   make([]float64, 0, 60),
		timeHistory:   make([]int64, 0, 60),
		currentCharge: 100.0,
		currentTemp:   25.0,
		historySize:   60,
	}
	// Заполняем начальными данными
	for i := 0; i < 10; i++ {
		b.chargeHistory = append(b.chargeHistory, b.currentCharge)
		b.tempHistory = append(b.tempHistory, b.currentTemp)
		b.timeHistory = append(b.timeHistory, time.Now().Unix())
	}
	return b
}

func (b *BatteryMonitor) update() {
	// Симуляция
	change := rand.Float64()*0.6 - 0.5
	b.currentCharge += change
	if b.currentCharge < 0 {
		b.currentCharge = 0
	}
	if b.currentCharge > 100 {
		b.currentCharge = 100
	}
	tempChange := rand.Float64()*7 - 2 + (100-b.currentCharge)*0.1
	b.currentTemp += tempChange
	if b.currentTemp < 20 {
		b.currentTemp = 20
	}
	if b.currentTemp > 60 {
		b.currentTemp = 60
	}

	// Время
	if len(b.chargeHistory) > 5 {
		rate := (b.chargeHistory[len(b.chargeHistory)-1] - b.chargeHistory[0]) / float64(len(b.chargeHistory))
		if rate < 0 {
			b.remainingTime = int(b.currentCharge / math.Abs(rate) * 60)
		} else {
			b.remainingTime = 999
		}
	} else {
		b.remainingTime = 999
	}

	// Добавляем в историю
	b.chargeHistory = append(b.chargeHistory, b.currentCharge)
	b.tempHistory = append(b.tempHistory, b.currentTemp)
	b.timeHistory = append(b.timeHistory, time.Now().Unix())
	if len(b.chargeHistory) > b.historySize {
		b.chargeHistory = b.chargeHistory[1:]
		b.tempHistory = b.tempHistory[1:]
		b.timeHistory = b.timeHistory[1:]
	}

	// Уведомления
	if b.currentCharge < 20 && !b.notifiedLow {
		b.notifiedLow = true
		fmt.Printf("\n🔋 Низкий заряд: %.1f%%! Подключите зарядку.\n", b.currentCharge)
	} else if b.currentCharge >= 25 {
		b.notifiedLow = false
	}
	if b.currentTemp > 50 && !b.notifiedHigh {
		b.notifiedHigh = true
		fmt.Printf("\n🌡️ Высокая температура: %.1f°C! Охладите устройство.\n", b.currentTemp)
	} else if b.currentTemp <= 45 {
		b.notifiedHigh = false
	}
}

func (b *BatteryMonitor) display() {
	// Очистка экрана (ANSI)
	fmt.Print("\033[H\033[2J")

	// Информация
	fmt.Printf("🔋 BatteryMonitor Pro — Go Edition\n")
	fmt.Printf("Заряд: %.1f%%  ", b.currentCharge)
	if b.remainingTime < 999 {
		fmt.Printf("Время: %dч %dмин  ", b.remainingTime/60, b.remainingTime%60)
	} else {
		fmt.Printf("Время: ∞  ")
	}
	fmt.Printf("Температура: %.1f°C\n", b.currentTemp)

	// Статус
	status := "Норма"
	color := "\033[32m" // green
	if b.currentCharge > 80 {
		status = "Отлично"
		color = "\033[32m"
	} else if b.currentCharge > 50 {
		status = "Хорошо"
		color = "\033[34m" // blue
	} else if b.currentCharge > 20 {
		status = "Нормально"
		color = "\033[33m" // yellow
	} else {
		status = "Критично!"
		color = "\033[31m" // red
	}
	fmt.Printf("Статус: %s%s\033[0m\n", color, status)

	// ASCII-график
	fmt.Println("\nГрафик заряда за последние 60 сек:")
	if len(b.chargeHistory) > 1 {
		const width = 50
		const height = 10
		maxVal := 105.0
		minVal := 0.0
		// Нормализуем данные
		data := b.chargeHistory
		step := float64(len(data)-1) / float64(width-1)
		// Строим матрицу
		grid := make([][]rune, height)
		for i := range grid {
			grid[i] = make([]rune, width)
			for j := range grid[i] {
				grid[i][j] = ' '
			}
		}
		// Заполняем точки
		for i := 0; i < width; i++ {
			idx := int(float64(i) * step)
			if idx >= len(data) {
				idx = len(data) - 1
			}
			val := data[idx]
			y := int((val - minVal) / (maxVal - minVal) * float64(height-1))
			if y >= height {
				y = height - 1
			}
			if y < 0 {
				y = 0
			}
			grid[height-1-y][i] = '█'
		}
		// Печать
		for i := 0; i < height; i++ {
			fmt.Printf("%3d%% ", int(float64(height-1-i)/float64(height-1)*100))
			for j := 0; j < width; j++ {
				if grid[i][j] == '█' {
					fmt.Print("\033[34m█\033[0m")
				} else {
					fmt.Print(" ")
				}
			}
			fmt.Println()
		}
	}
	fmt.Println("\nКоманды: refresh, export, reset, exit")
}

func (b *BatteryMonitor) exportCSV() {
	if len(b.chargeHistory) < 2 {
		fmt.Println("Недостаточно данных для экспорта")
		return
	}
	filename := fmt.Sprintf("battery_data_%d.csv", time.Now().Unix())
	file, err := os.Create(filename)
	if err != nil {
		fmt.Println("Ошибка создания файла:", err)
		return
	}
	defer file.Close()
	file.WriteString("Время,Заряд(%),Температура(°C)\n")
	for i := 0; i < len(b.chargeHistory); i++ {
		file.WriteString(fmt.Sprintf("%d,%.2f,%.2f\n", b.timeHistory[i], b.chargeHistory[i], b.tempHistory[i]))
	}
	fmt.Printf("Данные сохранены в %s\n", filename)
}

func (b *BatteryMonitor) reset() {
	b.chargeHistory = b.chargeHistory[:0]
	b.tempHistory = b.tempHistory[:0]
	b.timeHistory = b.timeHistory[:0]
	b.currentCharge = 100
	b.currentTemp = 25
	b.remainingTime = 0
	for i := 0; i < 10; i++ {
		b.chargeHistory = append(b.chargeHistory, b.currentCharge)
		b.tempHistory = append(b.tempHistory, b.currentTemp)
		b.timeHistory = append(b.timeHistory, time.Now().Unix())
	}
	fmt.Println("Данные сброшены")
}

func main() {
	rand.Seed(time.Now().UnixNano())
	monitor := NewBatteryMonitor()
	scanner := bufio.NewScanner(os.Stdin)
	fmt.Println("🔋 BatteryMonitor Pro — Go Edition")
	fmt.Println("Нажмите Enter для обновления, или введите команду: export, reset, exit")
	for {
		monitor.update()
		monitor.display()
		fmt.Print("> ")
		if !scanner.Scan() {
			break
		}
		line := strings.TrimSpace(scanner.Text())
		switch line {
		case "export":
			monitor.exportCSV()
		case "reset":
			monitor.reset()
		case "exit":
			fmt.Println("До свидания!")
			return
		default:
			// просто обновление
		}
	}
}
