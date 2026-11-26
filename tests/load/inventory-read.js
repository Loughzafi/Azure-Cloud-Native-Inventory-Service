import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';

const baseUrl = __ENV.INVENTORY_BASE_URL || 'http://127.0.0.1:8080';
const environment = __ENV.INVENTORY_ENVIRONMENT || '';
const token = __ENV.INVENTORY_TOKEN || '';
if (!/^http:\/\/(localhost|127\.0\.0\.1)(:\d{1,5})?$/.test(baseUrl)) {
  throw new Error('INVENTORY_BASE_URL must be an HTTP loopback origin without a path.');
}
if (!/^[A-Za-z0-9_-]{1,40}$/.test(environment)) {
  throw new Error('INVENTORY_ENVIRONMENT must be a short non-sensitive label.');
}
if (!token || /[\r\n]/.test(token)) {
  throw new Error('INVENTORY_TOKEN must contain a locally supplied bearer token.');
}

const completed = new Counter('inventory_completed');
export const options = {
  scenarios: {
    bounded_reads: {
      executor: 'shared-iterations',
      vus: 1,
      iterations: 20,
      maxDuration: '2m',
    },
  },
  thresholds: {
    inventory_completed: ['count==20'],
    checks: ['rate==1'],
    http_req_failed: ['rate==0'],
  },
};

export default function () {
  const response = http.get(`${baseUrl}/api/inventory`, {
    headers: { Authorization: `Bearer ${token}` },
    redirects: 0,
    timeout: '5s',
    responseType: 'none',
  });
  check(response, { 'inventory returns 200': (r) => r.status === 200 });
  completed.add(1);
  sleep(1);
}

export function handleSummary(data) {
  const metrics = data.metrics;
  const value = (name, field) => metrics[name]?.values?.[field] ?? 'n/a';
  return {
    stdout: [
      `UTC: ${new Date().toISOString()}`,
      `Environment: ${environment}; target: ${baseUrl}`,
      'Profile: read-only GET /api/inventory; 1 VU, 20 iterations, 1s pause, 5s request timeout',
      `Completed: ${value('inventory_completed', 'count')}/20; checks passed: ${value('checks', 'passes')}; checks failed: ${value('checks', 'fails')}`,
      `HTTP failure rate: ${value('http_req_failed', 'rate')}; request duration ms avg: ${value('http_req_duration', 'avg')}, p95: ${value('http_req_duration', 'p(95)')}, max: ${value('http_req_duration', 'max')}`,
      'Threshold failures result in a nonzero exit code.',
      '',
    ].join('\n'),
  };
}
