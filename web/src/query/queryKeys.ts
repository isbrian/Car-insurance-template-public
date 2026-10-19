export const queryKeys = {
  dashboard: {
    summary: ['dashboard', 'summary'] as const,
  },
  customers: {
    all: ['customers'] as const,
    lists: () => ['customers', 'list'] as const,
    list: (keyword: string, page: number, pageSize: number) => (
      ['customers', 'list', { keyword, page, pageSize }] as const
    ),
    details: () => ['customers', 'detail'] as const,
    detail: (id: number) => ['customers', 'detail', id] as const,
  },
  vehicles: {
    all: ['vehicles'] as const,
    lists: () => ['vehicles', 'list'] as const,
    list: (keyword: string, page: number, pageSize: number) => (
      ['vehicles', 'list', { keyword, page, pageSize }] as const
    ),
    details: () => ['vehicles', 'detail'] as const,
    detail: (id: number) => ['vehicles', 'detail', id] as const,
    byCustomer: (customerId: number) => ['vehicles', 'customer', customerId] as const,
  },
  rateVersions: {
    all: ['rate-versions'] as const,
  },
  quotes: {
    all: ['quotes'] as const,
  },
  policies: {
    all: ['policies'] as const,
    list: (keyword: string, status: string, page: number, pageSize: number) => (
      ['policies', 'list', { keyword, status, page, pageSize }] as const
    ),
    detail: (id: number) => ['policies', 'detail', id] as const,
    timeline: (id: number) => ['policies', 'timeline', id] as const,
  },
  auditLogs: {
    all: ['audit-logs'] as const,
    list: (query: object) => ['audit-logs', 'list', query] as const,
  },
  users: {
    all: ['users'] as const,
  },
  underwritingRules: {
    all: ['underwriting-rules'] as const,
  },
  permissionMatrix: {
    all: ['permission-matrix'] as const,
  },
  claims: {
    all: ['claims'] as const,
    list: (status: string) => ['claims', 'list', { status }] as const,
    detail: (id: number) => ['claims', 'detail', id] as const,
  },
  underwritingQueue: {
    all: ['underwriting-queue'] as const,
    list: (keyword: string, status: string, page: number, pageSize: number) => (
      ['underwriting-queue', 'list', { keyword, status, page, pageSize }] as const
    ),
    detail: (id: number) => ['underwriting-queue', 'detail', id] as const,
  },
  documents: {
    all: ['documents'] as const,
    cases: (query: object) => ['documents', 'cases', query] as const,
    pendingTotal: ['documents', 'pending-total'] as const,
    templates: ['documents', 'templates'] as const,
    template: (id: number) => ['documents', 'template', id] as const,
  },
  notifications: {
    all: ['notifications'] as const,
    inApp: ['notifications', 'in-app'] as const,
    history: (query: object) => ['notifications', 'history', query] as const,
  },
  payments: {
    all: ['payments'] as const,
    policyByApplication: (applicationId: number) => ['payments', 'policy', applicationId] as const,
  },
  applications: {
    all: ['applications'] as const,
    detail: (id: number) => ['applications', 'detail', id] as const,
  },
} as const
