// Chat Widget JavaScript
class ChatWidget {
    constructor() {
        this.isOpen = false;
        this.sessionId = localStorage.getItem('chatSessionId') || this.generateSessionId();
        this.messages = [];
        this.isTyping = false;
        
        this.init();
    }

    generateSessionId() {
        const sessionId = 'chat_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
        localStorage.setItem('chatSessionId', sessionId);
        return sessionId;
    }

    init() {
        this.createChatWidget();
        this.loadChatHistory();
        this.attachEventListeners();
    }

    createChatWidget() {
        const chatHTML = `
            <button class="chat-widget-toggle" id="chatToggle">
                <i class="fas fa-comment"></i>
            </button>

            <div class="chat-widget" id="chatWidget">
                <div class="chat-header">
                    <h6>🛍️ Shopping Assistant</h6>
                    <button class="chat-close" id="chatClose">&times;</button>
                </div>
                
                <div class="chat-messages" id="chatMessages">
                    <div class="chat-message assistant">
                        <div class="message-bubble assistant">
                            Hi! I'm your shopping assistant. I can help you find the perfect products based on your needs and preferences. What are you looking for today?
                        </div>
                    </div>
                </div>
                
                <div class="chat-input-container" style="position: relative;">
                    <textarea class="chat-input" id="chatInput" 
                             placeholder="Ask me about products, recommendations..." 
                             rows="1"></textarea>
                    <button class="chat-send-btn" id="chatSend">
                        <i class="fas fa-paper-plane"></i>
                    </button>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', chatHTML);
    }

    attachEventListeners() {
        const toggle = document.getElementById('chatToggle');
        const close = document.getElementById('chatClose');
        const input = document.getElementById('chatInput');
        const sendBtn = document.getElementById('chatSend');

        toggle.addEventListener('click', () => this.toggleChat());
        close.addEventListener('click', () => this.closeChat());
        sendBtn.addEventListener('click', () => this.sendMessage());
        
        input.addEventListener('keypress', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.sendMessage();
            }
        });

        // Auto-resize textarea
        input.addEventListener('input', () => {
            input.style.height = 'auto';
            input.style.height = Math.min(input.scrollHeight, 100) + 'px';
        });
    }

    toggleChat() {
        this.isOpen ? this.closeChat() : this.openChat();
    }

    openChat() {
        const widget = document.getElementById('chatWidget');
        const toggle = document.getElementById('chatToggle');
        
        widget.classList.add('open');
        toggle.classList.add('widget-open');
        toggle.innerHTML = '<i class="fas fa-times"></i>';
        this.isOpen = true;

        // Focus input
        setTimeout(() => {
            document.getElementById('chatInput').focus();
        }, 300);
    }

    closeChat() {
        const widget = document.getElementById('chatWidget');
        const toggle = document.getElementById('chatToggle');
        
        widget.classList.remove('open');
        toggle.classList.remove('widget-open');
        toggle.innerHTML = '<i class="fas fa-comment"></i>';
        this.isOpen = false;
    }

    async loadChatHistory() {
        try {
            const response = await fetch(`/api/chat/history/${this.sessionId}`);
            if (response.ok) {
                const history = await response.json();
                this.messages = history;
                this.renderMessages();
            }
        } catch (error) {
            console.error('Error loading chat history:', error);
        }
    }

    async sendMessage() {
        const input = document.getElementById('chatInput');
        const message = input.value.trim();
        
        if (!message || this.isTyping) return;

        // Add user message to UI
        this.addMessage('user', message);
        input.value = '';
        input.style.height = 'auto';

        // Show typing indicator
        this.showTyping();

        try {
            const currentPage = this.getCurrentPageContext();
            const response = await fetch('/api/chat/products', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    message: message,
                    sessionId: this.sessionId,
                    context: currentPage
                }),
            });

                        const data = await response.json();
                        if (data.reply) {
                            // Show assistant reply and attempt formatted table parse
                            this.addMessage('assistant', data.reply);
                            this.renderFormattedReply(data.reply);
                        } else {
                                this.addMessage('assistant', 'Sorry, I encountered an error. Please try again.');
                        }
        } catch (error) {
            console.error('Error sending message:', error);
            this.addMessage('assistant', 'Sorry, I\'m having trouble connecting. Please try again.');
        } finally {
            this.hideTyping();
        }
    }

        renderFormattedReply(replyText) {
                // Detect table markers TABLE_START ... TABLE_END
                const startIdx = replyText.indexOf('TABLE_START');
                const endIdx = replyText.indexOf('TABLE_END');
                if (startIdx === -1 || endIdx === -1 || endIdx <= startIdx) return; // no table

                const tableSection = replyText.substring(startIdx + 'TABLE_START'.length, endIdx).trim();
                // Expect header line followed by rows separated by newlines, pipe delimited
                const lines = tableSection.split(/\n+/).map(l => l.trim()).filter(l => l.length > 0);
                if (lines.length < 2) return;
                // Remove any markdown pipes alignment artifacts
                const header = lines[0];
                const rows = lines.slice(1);

                const headerCells = header.split('|').map(c => c.trim()).filter(c => c);
                if (headerCells.length < 5) return; // ensure expected columns

                const parsedRows = rows.map(r => r.split('|').map(c => c.trim()).filter(c => c));
                if (!parsedRows.length) return;

                const messagesContainer = document.getElementById('chatMessages');
                const wrapper = document.createElement('div');
                wrapper.className = 'chat-message assistant';

                const tableHtml = `
                <div class="message-bubble assistant">
                    <div class="product-table-container">
                        <div class="product-table-header"><span>Suggested Products (${parsedRows.length})</span></div>
                        <div class="product-table-wrapper">
                            <table class="product-table">
                                <thead><tr>${headerCells.map(h => `<th>${this.escapeHtml(h)}</th>`).join('')}</tr></thead>
                                <tbody>
                                    ${parsedRows.map(cols => `<tr>${cols.map((c,i) => {
                                            const cell = this.escapeHtml(c);
                                            if (headerCells[i].toLowerCase()==='price' && /^£?\d/.test(cell)) {
                                                    const num = cell.replace(/[^0-9.]/g,'');
                                                    return `<td class="price-cell">£${Number(num).toFixed(2)}</td>`;
                                            }
                                            if (headerCells[i].toLowerCase()==='category') {
                                                    return `<td><span class="category-pill">${cell}</span></td>`;
                                            }
                                            return `<td>${cell}</td>`;
                                    }).join('')}</tr>`).join('')}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
                <div class="message-time">${new Date().toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}</div>`;

                wrapper.innerHTML = tableHtml;
                messagesContainer.appendChild(wrapper);
                messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }

        escapeHtml(str) {
                return str.replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;','\'':'&#39;'}[ch]));
        }

    addMessage(role, content, recommendations = null) {
        const messagesContainer = document.getElementById('chatMessages');
        const messageDiv = document.createElement('div');
        messageDiv.className = `chat-message ${role}`;

        let recommendationsHTML = '';
        if (recommendations && recommendations.length > 0) {
            recommendationsHTML = `
                <div class="product-recommendations">
                    ${recommendations.map(rec => `
                        <div class="recommendation-card">
                            <div class="recommendation-header">
                                <h6 class="recommendation-title">${rec.name}</h6>
                                <span class="recommendation-price">£${rec.price.toFixed(2)}</span>
                            </div>
                            <div class="recommendation-reason">${rec.reason}</div>
                            <div class="recommendation-actions">
                                <a href="/Products" class="btn-recommendation btn-view">
                                    <i class="fas fa-eye"></i> View
                                </a>
                                <button class="btn-recommendation btn-add-cart" onclick="chatWidget.addToCart(${rec.productId})">
                                    <i class="fas fa-shopping-cart"></i> Add to Cart
                                </button>
                            </div>
                        </div>
                    `).join('')}
                </div>
            `;
        }

        messageDiv.innerHTML = `
            <div class="message-bubble ${role}">
                ${content}
                ${recommendationsHTML}
            </div>
            <div class="message-time">
                ${new Date().toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}
            </div>
        `;

        messagesContainer.appendChild(messageDiv);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    showTyping() {
        this.isTyping = true;
        const messagesContainer = document.getElementById('chatMessages');
        const typingDiv = document.createElement('div');
        typingDiv.className = 'typing-indicator';
        typingDiv.id = 'typingIndicator';
        typingDiv.innerHTML = `
            Assistant is typing
            <div class="typing-dots">
                <div class="typing-dot"></div>
                <div class="typing-dot"></div>
                <div class="typing-dot"></div>
            </div>
        `;
        
        messagesContainer.appendChild(typingDiv);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    hideTyping() {
        this.isTyping = false;
        const typingIndicator = document.getElementById('typingIndicator');
        if (typingIndicator) {
            typingIndicator.remove();
        }
    }

    renderMessages() {
        const messagesContainer = document.getElementById('chatMessages');
        messagesContainer.innerHTML = '';
        
        // Add welcome message
        this.addMessage('assistant', 'Hi! I\'m your shopping assistant. I can help you find the perfect products based on your needs and preferences. What are you looking for today?');
        
        // Add chat history
        this.messages.forEach(msg => {
            this.addMessage(msg.role, msg.content);
        });
    }

    getCurrentPageContext() {
        const path = window.location.pathname;
        if (path.includes('/Products')) return 'products';
        if (path.includes('/Cart')) return 'cart';
        if (path.includes('/Checkout')) return 'checkout';
        return 'home';
    }

    async addToCart(productId) {
        try {
            // This would integrate with your existing cart service
            const response = await fetch('/api/cart/add', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    productId: productId,
                    quantity: 1
                }),
            });

            if (response.ok) {
                this.addMessage('assistant', '✅ Product added to cart! You can continue shopping or proceed to checkout.');
                // Update cart UI if needed
                window.location.reload();
            } else {
                this.addMessage('assistant', '❌ Sorry, I couldn\'t add that product to your cart. Please try again.');
            }
        } catch (error) {
            console.error('Error adding to cart:', error);
            this.addMessage('assistant', '❌ Sorry, I couldn\'t add that product to your cart. Please try again.');
        }
    }
}

// Initialize chat widget when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    window.chatWidget = new ChatWidget();
});