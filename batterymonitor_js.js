// batterymonitor_js.js — монитор батареи с графиками на JavaScript (Node.js + readline)

const readline = require('readline');
const fs = require('fs');
const { clear } = require('console');

const HISTORY_SIZE = 60;
let chargeHistory = [];
let tempHistory = [];
let timeHistory = [];
let currentCharge = 100;
let currentTemp = 25;
let remainingTime = 999;
let notifiedLow = false;
let notifiedHigh = false;

function currentTime() {
    return Math.floor(Date.now() / 1000);
}

function update() {
    // Симуляция
    const change = Math.random() * 0.6 - 0.5;
    currentCharge += change;
    currentCharge = Math.max(0, Math.min(100, currentCharge));
    const tempChange = Math.random() * 7 - 2 + (100 - currentCharge) * 0.1;
    currentTemp += tempChange;
    currentTemp = Math.max(20, Math.min(60, currentTemp));

    // Время
    if (chargeHistory.length > 5) {
        const rate = (chargeHistory[chargeHistory.length-1] - chargeHistory[0]) / chargeHistory.length;
        if (rate < 0) {
            remainingTime = Math.floor(currentCharge / Math.abs(rate) * 60);
        } else {
            remainingTime = 999;
        }
    } else {
        remainingTime = 999;
    }

    // История
    chargeHistory.push(currentCharge);
    tempHistory.push(currentTemp);
    timeHistory.push(currentTime());
    if (chargeHistory.length > HISTORY_SIZE) {
        chargeHistory.shift();
        tempHistory.shift();
        timeHistory.shift();
    }

    // Уведомления
    if (currentCharge < 20 && !notifiedLow) {
        notifiedLow = true;
        console.log(`\n🔋 Низкий заряд: ${currentCharge.toFixed(1)}%! Подключите зарядку.`);
    } else if (currentCharge >= 25) {
        notifiedLow = false;
    }
    if (currentTemp > 50 && !notifiedHigh) {
        notifiedHigh = true;
        console.log(`\n🌡️ Высокая температура: ${currentTemp.toFixed(1)}°C! Охладите устройство.`);
    } else if (currentTemp <= 45) {
        notifiedHigh = false;
    }
}

function draw() {
    console.clear();
    console.log('🔋 BatteryMonitor Pro — JavaScript Edition');
    console.log(`Заряд: ${currentCharge.toFixed(1)}%  ` +
        (remainingTime < 999 ? `Время: ${Math.floor(remainingTime/60)}ч ${remainingTime%60}мин  ` : 'Время: ∞  ') +
        `Температура: ${currentTemp.toFixed(1)}°C`);

    // Статус
    let status = 'Норма', color = '\x1b[32m';
    if (currentCharge > 80) { status = 'Отлично'; color = '\x1b[32m'; }
    else if (currentCharge > 50) { status = 'Хорошо'; color = '\x1b[34m'; }
    else if (currentCharge > 20) { status = 'Нормально'; color = '\x1b[33m'; }
    else { status = 'Критично!'; color = '\x1b[31m'; }
    console.log(`Статус: ${color}${status}\x1b[0m`);

    // ASCII график
    console.log('\nГрафик заряда за последние 60 сек:');
    if (chargeHistory.length > 1) {
        const width = 50, height = 10;
        const maxVal = 105, minVal = 0;
        const data = chargeHistory;
        const step = (data.length - 1) / (width - 1);
        const grid = Array(height).fill().map(() => Array(width).fill(' '));
        for (let i = 0; i < width; i++) {
            const idx = Math.min(Math.floor(i * step), data.length - 1);
            const val = data[idx];
            let y = Math.floor((val - minVal) / (maxVal - minVal) * (height - 1));
            y = Math.min(y, height - 1);
            grid[height - 1 - y][i] = '█';
        }
        for (let i = 0; i < height; i++) {
            process.stdout.write(`${String((height-1-i)*100/(height-1)).padStart(3)}% `);
            for (let j = 0; j < width; j++) {
                if (grid[i][j] === '█') {
                    process.stdout.write('\x1b[34m█\x1b[0m');
                } else {
                    process.stdout.write(' ');
                }
            }
            console.log();
        }
    }
    console.log('\nКоманды: export, reset, exit');
}

function exportCSV() {
    if (chargeHistory.length < 2) {
        console.log('Недостаточно данных для экспорта');
        return;
    }
    const filename = `battery_data_${Date.now()}.csv`;
    let csv = 'Время,Заряд(%),Температура(°C)\n';
    for (let i = 0; i < chargeHistory.length; i++) {
        csv += `${timeHistory[i]},${chargeHistory[i].toFixed(2)},${tempHistory[i].toFixed(2)}\n`;
    }
    fs.writeFileSync(filename, csv);
    console.log(`Данные сохранены в ${filename}`);
}

function resetData() {
    chargeHistory = [];
    tempHistory = [];
    timeHistory = [];
    currentCharge = 100;
    currentTemp = 25;
    remainingTime = 999;
    for (let i = 0; i < 10; i++) {
        chargeHistory.push(currentCharge);
        tempHistory.push(currentTemp);
        timeHistory.push(currentTime());
    }
    console.log('Данные сброшены');
}

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    prompt: '> '
});

function interactive() {
    console.log('🔋 BatteryMonitor Pro — JavaScript Edition');
    console.log('Нажмите Enter для обновления, или введите команду: export, reset, exit');
    rl.prompt();

    // Обновление каждую секунду в фоне
    setInterval(() => {
        update();
        draw();
        rl.prompt();
    }, 1000);

    rl.on('line', (line) => {
        const cmd = line.trim().toLowerCase();
        switch (cmd) {
            case 'export': exportCSV(); break;
            case 'reset': resetData(); break;
            case 'exit':
                console.log('До свидания!');
                rl.close();
                process.exit(0);
            default:
                // просто обновление
        }
        rl.prompt();
    }).on('close', () => {
        process.exit(0);
    });
}

interactive();
