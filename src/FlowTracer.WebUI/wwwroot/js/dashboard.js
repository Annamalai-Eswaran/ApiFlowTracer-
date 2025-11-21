// Dashboard State
const state = {
    traces: [],
    filteredTraces: [],
    connection: null,
    isPaused: false,
    autoScroll: true,
    filters: {
        search: '',
        type: 'all',
        correlation: 'all',
        time: 'all'
    }
};

// Initialize Dashboard
document.addEventListener('DOMContentLoaded', () => {
    initializeTheme();
    initializeEventListeners();
    initializeSignalR();
    loadInitialTraces();
    startStatsUpdater();
    startTimeUpdater();
});

// Theme Management
function initializeTheme() {
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'dark' || (!savedTheme && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
        document.body.classList.add('dark-mode');
        document.getElementById('themeIcon').textContent = '☀️';
    }
}

function toggleTheme() {
    document.body.classList.toggle('dark-mode');
    const isDark = document.body.classList.contains('dark-mode');
    document.getElementById('themeIcon').textContent = isDark ? '☀️' : '🌙';
    localStorage.setItem('theme', isDark ? 'dark' : 'light');
}

// Event Listeners
function initializeEventListeners() {
    document.getElementById('toggleTheme').addEventListener('click', toggleTheme);
    document.getElementById('clearBtn').addEventListener('click', clearAllTraces);
    document.getElementById('exportBtn').addEventListener('click', exportTraces);
    document.getElementById('pauseBtn').addEventListener('click', togglePause);
    document.getElementById('searchInput').addEventListener('input', debounce(handleSearch, 300));
    document.getElementById('typeFilter').addEventListener('change', handleFilterChange);
    document.getElementById('correlationFilter').addEventListener('change', handleFilterChange);
    document.getElementById('timeFilter').addEventListener('change', handleFilterChange);
    document.getElementById('closeModal').addEventListener('click', closeModal);
    
    // Keyboard shortcuts
    document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
            e.preventDefault();
            document.getElementById('searchInput').focus();
        }
        if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
            e.preventDefault();
            clearAllTraces();
        }
    });

    // Close modal on outside click
    document.getElementById('detailModal').addEventListener('click', (e) => {
        if (e.target.id === 'detailModal') {
            closeModal();
        }
    });
}

// SignalR Connection
async function initializeSignalR() {
    try {
        state.connection = new signalR.HubConnectionBuilder()
            .withUrl('/tracehub')
            .withAutomaticReconnect()
            .build();

        state.connection.on('ReceiveTrace', (trace) => {
            handleNewTrace(trace);
        });

        state.connection.onreconnecting(() => {
            updateConnectionStatus('Reconnecting...', false);
        });

        state.connection.onreconnected(() => {
            updateConnectionStatus('Connected', true);
            showToast('Reconnected to dashboard', 'success');
        });

        state.connection.onclose(() => {
            updateConnectionStatus('Disconnected', false);
            showToast('Connection lost. Attempting to reconnect...', 'error');
        });

        await state.connection.start();
        updateConnectionStatus('Connected', true);
        console.log('✅ SignalR connected');
    } catch (err) {
        console.error('❌ SignalR connection failed:', err);
        updateConnectionStatus('Connection Failed', false);
        showToast('Failed to connect. Falling back to polling...', 'warning');
        startPolling();
    }
}

function updateConnectionStatus(text, isConnected) {
    document.getElementById('statusText').textContent = text;
    const indicator = document.getElementById('statusIndicator');
    if (isConnected) {
        indicator.classList.add('connected');
    } else {
        indicator.classList.remove('connected');
    }
}

// Load Initial Traces
async function loadInitialTraces() {
    try {
        const response = await fetch('/api/traces');
        const traces = await response.json();
        state.traces = traces || [];
        updateCorrelationFilter();
        applyFilters();
        renderTraces();
    } catch (err) {
        console.error('Failed to load initial traces:', err);
        showToast('Failed to load traces', 'error');
    }
}

// Handle New Trace (Real-time)
function handleNewTrace(trace) {
    if (!state.isPaused) {
        state.traces.unshift(trace);
        
        // Limit traces to buffer size
        if (state.traces.length > 500) {
            state.traces.pop();
        }

        updateCorrelationFilter();
        applyFilters();
        
        // Only re-render if trace passes filters
        if (state.filteredTraces.includes(trace)) {
            renderNewTrace(trace);
            
            if (state.autoScroll) {
                scrollToTop();
            }
        }

        updateStats();
    }
}

// Filtering
function applyFilters() {
    state.filteredTraces = state.traces.filter(trace => {
        // Search filter
        if (state.filters.search) {
            const searchLower = state.filters.search.toLowerCase();
            const searchableText = JSON.stringify(trace).toLowerCase();
            if (!searchableText.includes(searchLower)) {
                return false;
            }
        }

        // Type filter
        if (state.filters.type !== 'all') {
            if (state.filters.type === 'http' && trace.kind !== 0) return false;
            if (state.filters.type === 'database' && trace.kind !== 1) return false;
        }

        // Correlation filter
        if (state.filters.correlation !== 'all' && trace.correlationId !== state.filters.correlation) {
            return false;
        }

        // Time filter
        if (state.filters.time !== 'all') {
            const seconds = parseInt(state.filters.time);
            const traceTime = new Date(trace.timestampUtc);
            const cutoff = new Date(Date.now() - seconds * 1000);
            if (traceTime < cutoff) return false;
        }

        return true;
    });
}

function handleSearch(e) {
    state.filters.search = e.target.value;
    applyFilters();
    renderTraces();
}

function handleFilterChange(e) {
    const filterId = e.target.id;
    if (filterId === 'typeFilter') state.filters.type = e.target.value;
    if (filterId === 'correlationFilter') state.filters.correlation = e.target.value;
    if (filterId === 'timeFilter') state.filters.time = e.target.value;
    
    applyFilters();
    renderTraces();
}

// Render Traces
function renderTraces() {
    const container = document.getElementById('tracesList');
    const emptyState = document.getElementById('emptyState');
    
    if (state.filteredTraces.length === 0) {
        container.innerHTML = '';
        emptyState.style.display = 'block';
    } else {
        emptyState.style.display = 'none';
        container.innerHTML = state.filteredTraces.map(trace => createTraceCard(trace)).join('');
        
        // Add click listeners
        container.querySelectorAll('.trace-card').forEach((card, index) => {
            card.addEventListener('click', () => showTraceDetails(state.filteredTraces[index]));
        });
    }
}

function renderNewTrace(trace) {
    const container = document.getElementById('tracesList');
    const emptyState = document.getElementById('emptyState');
    
    emptyState.style.display = 'none';
    
    const traceHtml = createTraceCard(trace);
    container.insertAdjacentHTML('afterbegin', traceHtml);
    
    // Add click listener
    const newCard = container.firstElementChild;
    newCard.addEventListener('click', () => showTraceDetails(trace));
}

// Create Trace Card HTML
function createTraceCard(trace) {
    const isHttp = trace.kind === 0;
    const isDb = trace.kind === 1;
    
    const icon = isHttp ? '🌐' : '🗄️';
    const durationClass = trace.durationMs < 100 ? 'fast' : trace.durationMs < 500 ? 'medium' : 'slow';
    const relativeTime = getRelativeTime(trace.timestampUtc);
    
    let detailsHtml = '';
    
    if (isHttp && trace.http) {
        const statusClass = getStatusClass(trace.http.statusCode);
        detailsHtml = `
            <div class="http-details">
                <div class="http-method-row">
                    <span class="method-badge method-${trace.http.method}">${trace.http.method}</span>
                    <span class="status-badge ${statusClass}">${trace.http.statusCode}</span>
                    <span class="http-url">${escapeHtml(trace.http.url)}</span>
                </div>
                ${trace.location ? `<div class="code-location">📂 ${escapeHtml(trace.location.filePath)}:${trace.location.lineNumber}</div>` : ''}
            </div>
        `;
    } else if (isDb && trace.database) {
        const queryKind = getQueryKind(trace.database.kind);
        detailsHtml = `
            <div class="db-details">
                <div class="db-query-row">
                    <span class="query-badge query-${queryKind}">${queryKind}</span>
                    ${trace.database.rowsAffected !== null ? `<span class="rows-badge">${trace.database.rowsAffected} rows</span>` : ''}
                </div>
                <div class="sql-query">${escapeHtml(trace.database.sqlQuery)}</div>
                ${trace.location ? `<div class="code-location">📂 ${escapeHtml(trace.location.filePath)}:${trace.location.lineNumber}</div>` : ''}
            </div>
        `;
    }
    
    return `
        <div class="trace-card" data-trace-id="${trace.id}">
            <div class="trace-header">
                <div class="trace-meta">
                    <span class="sequence-badge">#${trace.sequenceNumber}</span>
                    <span class="trace-icon">${icon}</span>
                    <span class="trace-time" data-timestamp="${trace.timestampUtc}">${relativeTime}</span>
                </div>
                <span class="duration-badge ${durationClass}">${trace.durationMs}ms</span>
            </div>
            ${detailsHtml}
        </div>
    `;
}

// Show Trace Details Modal
function showTraceDetails(trace) {
    const modal = document.getElementById('detailModal');
    const modalBody = document.getElementById('modalBody');
    const modalTitle = document.getElementById('modalTitle');
    
    const isHttp = trace.kind === 0;
    modalTitle.textContent = isHttp ? '🌐 HTTP Request Details' : '🗄️ Database Query Details';
    
    let detailsHtml = `
        <div style="margin-bottom: 1.5rem;">
            <h3 style="margin-bottom: 0.5rem;">Basic Info</h3>
            <p><strong>Sequence:</strong> #${trace.sequenceNumber}</p>
            <p><strong>Timestamp:</strong> ${new Date(trace.timestampUtc).toLocaleString()}</p>
            <p><strong>Duration:</strong> ${trace.durationMs}ms</p>
            <p><strong>Correlation ID:</strong> ${trace.correlationId}</p>
            ${trace.location ? `<p><strong>Code Location:</strong> ${escapeHtml(trace.location.filePath)}:${trace.location.lineNumber} <button class="copy-btn" onclick="copyToClipboard('${escapeHtml(trace.location.filePath)}:${trace.location.lineNumber}')">Copy</button></p>` : ''}
        </div>
    `;
    
    if (isHttp && trace.http) {
        detailsHtml += `
            <div style="margin-bottom: 1.5rem;">
                <h3 style="margin-bottom: 0.5rem;">HTTP Details</h3>
                <p><strong>Method:</strong> ${trace.http.method}</p>
                <p><strong>URL:</strong> ${escapeHtml(trace.http.url)} <button class="copy-btn" onclick="copyToClipboard('${escapeHtml(trace.http.url)}')">Copy</button></p>
                <p><strong>Status Code:</strong> ${trace.http.statusCode}</p>
            </div>
            ${trace.http.requestBody ? `
            <div style="margin-bottom: 1.5rem;">
                <h3 style="margin-bottom: 0.5rem;">Request Body <button class="copy-btn" onclick="copyToClipboard('${escapeHtml(trace.http.requestBody)}')">Copy</button></h3>
                <pre class="json-content">${formatJson(trace.http.requestBody)}</pre>
            </div>` : ''}
            ${trace.http.responseBody ? `
            <div style="margin-bottom: 1.5rem;">
                <h3 style="margin-bottom: 0.5rem;">Response Body <button class="copy-btn" onclick="copyToClipboard('${escapeHtml(trace.http.responseBody)}')">Copy</button></h3>
                <pre class="json-content">${formatJson(trace.http.responseBody)}</pre>
            </div>` : ''}
        `;
    } else if (trace.database) {
        detailsHtml += `
            <div style="margin-bottom: 1.5rem;">
                <h3 style="margin-bottom: 0.5rem;">Database Details</h3>
                <p><strong>Query Type:</strong> ${getQueryKind(trace.database.kind)}</p>
                <p><strong>Rows Affected:</strong> ${trace.database.rowsAffected ?? 'N/A'}</p>
                ${trace.database.databaseName ? `<p><strong>Database:</strong> ${trace.database.databaseName}</p>` : ''}
            </div>
            <div style="margin-bottom: 1.5rem;">
                <h3 style="margin-bottom: 0.5rem;">SQL Query <button class="copy-btn" onclick="copyToClipboard('${escapeHtml(trace.database.sqlQuery)}')">Copy</button></h3>
                <pre class="sql-query">${escapeHtml(trace.database.sqlQuery)}</pre>
            </div>
            ${Object.keys(trace.database.parameters).length > 0 ? `
            <div style="margin-bottom: 1.5rem;">
                <h3 style="margin-bottom: 0.5rem;">Parameters</h3>
                <pre class="db-params">${JSON.stringify(trace.database.parameters, null, 2)}</pre>
            </div>` : ''}
        `;
    }
    
    modalBody.innerHTML = detailsHtml;
    modal.classList.add('show');
}

function closeModal() {
    document.getElementById('detailModal').classList.remove('show');
}

// Stats Update
async function updateStats() {
    try {
        const response = await fetch('/api/stats');
        const stats = await response.json();
        
        document.getElementById('totalTraces').textContent = stats.totalTraces;
        document.getElementById('httpCalls').textContent = stats.httpCalls;
        document.getElementById('dbQueries').textContent = stats.dbQueries;
        document.getElementById('avgDuration').textContent = `${stats.avgDurationMs}ms`;
    } catch (err) {
        console.error('Failed to update stats:', err);
    }
}

function startStatsUpdater() {
    updateStats();
    setInterval(updateStats, 2000);
}

// Time Updater (for relative times)
function startTimeUpdater() {
    setInterval(() => {
        document.querySelectorAll('.trace-time').forEach(el => {
            const timestamp = el.dataset.timestamp;
            if (timestamp) {
                el.textContent = getRelativeTime(timestamp);
            }
        });
    }, 1000);
}

// Actions
async function clearAllTraces() {
    if (!confirm('Are you sure you want to clear all traces?')) return;
    
    try {
        await fetch('/api/traces', { method: 'DELETE' });
        state.traces = [];
        state.filteredTraces = [];
        renderTraces();
        updateStats();
        updateCorrelationFilter();
        showToast('All traces cleared', 'success');
    } catch (err) {
        console.error('Failed to clear traces:', err);
        showToast('Failed to clear traces', 'error');
    }
}

function exportTraces() {
    const dataStr = JSON.stringify(state.filteredTraces, null, 2);
    const blob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `apiflowtracer-${new Date().toISOString()}.json`;
    link.click();
    URL.revokeObjectURL(url);
    showToast('Traces exported successfully', 'success');
}

function togglePause() {
    state.isPaused = !state.isPaused;
    const pauseIcon = document.getElementById('pauseIcon');
    const pauseText = document.getElementById('pauseText');
    
    if (state.isPaused) {
        pauseIcon.textContent = '▶️';
        pauseText.textContent = 'Resume';
        showToast('Auto-update paused', 'info');
    } else {
        pauseIcon.textContent = '⏸️';
        pauseText.textContent = 'Pause';
        showToast('Auto-update resumed', 'info');
    }
}

// Correlation Filter Update
function updateCorrelationFilter() {
    const select = document.getElementById('correlationFilter');
    const currentValue = select.value;
    
    const correlationIds = [...new Set(state.traces.map(t => t.correlationId))].filter(id => id);
    
    select.innerHTML = '<option value="all">All Correlations</option>';
    correlationIds.forEach(id => {
        const option = document.createElement('option');
        option.value = id;
        option.textContent = id.substring(0, 8) + '...';
        select.appendChild(option);
    });
    
    if (correlationIds.includes(currentValue)) {
        select.value = currentValue;
    }
}

// Polling Fallback
function startPolling() {
    setInterval(async () => {
        if (!state.connection || state.connection.state !== 'Connected') {
            await loadInitialTraces();
        }
    }, 5000);
}

// Utility Functions
function getRelativeTime(timestamp) {
    const now = Date.now();
    const then = new Date(timestamp).getTime();
    const diff = Math.floor((now - then) / 1000);
    
    if (diff < 60) return `${diff}s ago`;
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return `${Math.floor(diff / 86400)}d ago`;
}

function getStatusClass(statusCode) {
    if (statusCode >= 200 && statusCode < 300) return 'status-2xx';
    if (statusCode >= 300 && statusCode < 400) return 'status-3xx';
    if (statusCode >= 400 && statusCode < 500) return 'status-4xx';
    if (statusCode >= 500) return 'status-5xx';
    return '';
}

function getQueryKind(kind) {
    const kinds = ['SELECT', 'INSERT', 'UPDATE', 'DELETE', 'OTHER'];
    return kinds[kind] || 'OTHER';
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function formatJson(str) {
    try {
        const obj = JSON.parse(str);
        return escapeHtml(JSON.stringify(obj, null, 2));
    } catch {
        return escapeHtml(str);
    }
}

function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

function scrollToTop() {
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.textContent = message;
    
    container.appendChild(toast);
    
    setTimeout(() => {
        toast.style.animation = 'slideInRight 0.3s ease reverse';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(() => {
        showToast('Copied to clipboard', 'success');
    }).catch(() => {
        showToast('Failed to copy', 'error');
    });
}

// Make copyToClipboard available globally for inline onclick
window.copyToClipboard = copyToClipboard;
