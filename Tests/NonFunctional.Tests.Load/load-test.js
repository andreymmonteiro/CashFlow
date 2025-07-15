import http from 'k6/http';
import { sleep, check, fail } from 'k6';
import { Rate } from 'k6/metrics';

export let errorRate = new Rate('errors');

export let options = {
    insecureSkipTLSVerify: true,
    stages: [
        // Warm-up
        { duration: '30s', target: 25 }, // ramp to 25 RPS
        { duration: '30s', target: 50 }, // ramp to 50 RPS
        // Test at peak
        { duration: '2m', target: 50 },  // hold 50 RPS
        // Ramp down
        { duration: '30s', target: 0 },
    ],
    thresholds: {
        'errors': ['rate<=0.05'], // ≤ 5% error rate
        'http_req_duration': ['p(95)<500'], // 95% of requests <500ms (adjust if needed)
    },
    insecureSkipTLSVerify: true, // allow self-signed localhost certs
};

function randomAmount(min, max) {
    let amount = 0;
    while (amount === 0) {
        amount = Math.floor(Math.random() * (max - min + 1)) + min;
    }
    return amount;
}

// Generate random UUID v4 (simplified, random)
function randomUUID() {
    // Generate 16 random bytes and convert to UUID v4 format
    let hex = [...Array(16)].map(() => Math.floor(Math.random() * 256));
    hex[6] = (hex[6] & 0x0f) | 0x40; // version 4
    hex[8] = (hex[8] & 0x3f) | 0x80; // variant 10

    function byteToHex(byte) {
        return ('0' + byte.toString(16)).slice(-2);
    }

    return [
        byteToHex(hex[0]), byteToHex(hex[1]), byteToHex(hex[2]), byteToHex(hex[3]),
        '-',
        byteToHex(hex[4]), byteToHex(hex[5]),
        '-',
        byteToHex(hex[6]), byteToHex(hex[7]),
        '-',
        byteToHex(hex[8]), byteToHex(hex[9]),
        '-',
        byteToHex(hex[10]), byteToHex(hex[11]), byteToHex(hex[12]), byteToHex(hex[13]), byteToHex(hex[14]), byteToHex(hex[15])
    ].join('');
}

export default function () {
    const url = 'https://transactionservice/api/transactions';
    const payload = JSON.stringify({
        accountId: randomUUID(),
        amount: randomAmount(-10000, 10000),
    });
    const params = {
        headers: {
            'Content-Type': 'application/json',
            'accept': 'application/json',
        },
    };

    let res = http.post(url, payload, params);

    const success = check(res, {
        'status is 200': (r) => r.status === 200,
    });

    errorRate.add(!success);

    sleep(1 / 50); // pace to ~50 RPS
}
