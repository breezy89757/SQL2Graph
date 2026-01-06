/**
 * SQL2Graph - Graph Visualization using Cytoscape.js
 * Force-directed graph with interactive nodes and edges
 */

// Load Cytoscape.js from CDN
(function () {
    if (!window.cytoscape) {
        const script = document.createElement('script');
        script.src = 'https://cdnjs.cloudflare.com/ajax/libs/cytoscape/3.28.1/cytoscape.min.js';
        script.async = true;
        document.head.appendChild(script);
    }
})();

window.graphVisualizer = {
    cy: null,
    selectedNode: null,
    selectedEdge: null,

    /**
     * Initialize the graph visualization
     * @param {string} containerId - The container element ID
     * @param {object} data - Graph data with nodes and edges
     */
    init: function (containerId, data) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error('Container not found:', containerId);
            return;
        }

        // Wait for Cytoscape to load
        if (!window.cytoscape) {
            setTimeout(() => this.init(containerId, data), 100);
            return;
        }

        // Convert data to Cytoscape format
        const elements = this.convertToElements(data);

        // Initialize Cytoscape
        this.cy = cytoscape({
            container: container,
            elements: elements,
            style: this.getStyles(),
            layout: {
                name: 'cose',
                animate: true,
                animationDuration: 1000,
                nodeRepulsion: function (node) { return 8000; },
                idealEdgeLength: function (edge) { return 150; },
                edgeElasticity: function (edge) { return 100; },
                gravity: 0.25,
                numIter: 1000,
                padding: 50
            },
            minZoom: 0.3,
            maxZoom: 3,
            wheelSensitivity: 0.3
        });

        // Setup event handlers
        this.setupEventHandlers();

        console.log('Graph initialized with', data.nodes.length, 'nodes and', data.edges.length, 'edges');
    },

    /**
     * Convert our data format to Cytoscape elements
     */
    convertToElements: function (data) {
        const elements = [];

        // Add nodes
        data.nodes.forEach(node => {
            elements.push({
                data: {
                    id: node.id,
                    label: node.label,
                    sourceTable: node.sourceTable,
                    description: node.description,
                    properties: node.properties,
                    color: node.color
                },
                position: { x: node.x, y: node.y }
            });
        });

        // Add edges
        data.edges.forEach(edge => {
            elements.push({
                data: {
                    id: edge.id,
                    source: edge.source,
                    target: edge.target,
                    type: edge.type,
                    sourceTable: edge.sourceTable,
                    description: edge.description,
                    isJoinTable: edge.isJoinTable,
                    properties: edge.properties
                }
            });
        });

        return elements;
    },

    /**
     * Get Cytoscape styles
     */
    getStyles: function () {
        return [
            // Node styles
            {
                selector: 'node',
                style: {
                    'background-color': 'data(color)',
                    'label': 'data(label)',
                    'text-valign': 'bottom',
                    'text-halign': 'center',
                    'text-margin-y': 8,
                    'color': '#f1f5f9',
                    'font-size': '14px',
                    'font-weight': 600,
                    'font-family': 'Inter, sans-serif',
                    'width': 60,
                    'height': 60,
                    'border-width': 3,
                    'border-color': '#1a1a2e',
                    'text-outline-color': '#0f0f1a',
                    'text-outline-width': 2,
                    'transition-property': 'width, height, border-width, border-color',
                    'transition-duration': '0.2s'
                }
            },
            // Node hover
            {
                selector: 'node:active, node:selected',
                style: {
                    'width': 75,
                    'height': 75,
                    'border-width': 4,
                    'border-color': '#00d4ff',
                    'z-index': 999
                }
            },
            // Edge styles
            {
                selector: 'edge',
                style: {
                    'width': 3,
                    'line-color': '#64748b',
                    'target-arrow-color': '#64748b',
                    'target-arrow-shape': 'triangle',
                    'curve-style': 'bezier',
                    'label': 'data(type)',
                    'font-size': '11px',
                    'font-weight': 500,
                    'font-family': 'Fira Code, monospace',
                    'color': '#f472b6',
                    'text-rotation': 'autorotate',
                    'text-margin-y': -10,
                    'text-outline-color': '#0f0f1a',
                    'text-outline-width': 2,
                    'transition-property': 'width, line-color, target-arrow-color',
                    'transition-duration': '0.2s'
                }
            },
            // Edge hover
            {
                selector: 'edge:active, edge:selected',
                style: {
                    'width': 5,
                    'line-color': '#00d4ff',
                    'target-arrow-color': '#00d4ff',
                    'z-index': 999
                }
            },
            // Connected edges highlight
            {
                selector: '.highlighted',
                style: {
                    'width': 4,
                    'line-color': '#00d4ff',
                    'target-arrow-color': '#00d4ff'
                }
            },
            // Join table edges (dashed)
            {
                selector: 'edge[?isJoinTable]',
                style: {
                    'line-style': 'dashed',
                    'line-dash-pattern': [6, 3]
                }
            }
        ];
    },

    /**
     * Setup event handlers for interactivity
     */
    setupEventHandlers: function () {
        const cy = this.cy;

        // Node tap - show details
        cy.on('tap', 'node', (event) => {
            const node = event.target;
            this.selectedNode = node.data();
            this.selectedEdge = null;

            // Highlight connected edges
            cy.edges().removeClass('highlighted');
            node.connectedEdges().addClass('highlighted');

            // Dispatch event for Blazor
            this.dispatchNodeSelected(node.data());
        });

        // Edge tap - show details
        cy.on('tap', 'edge', (event) => {
            const edge = event.target;
            this.selectedEdge = edge.data();
            this.selectedNode = null;

            // Highlight this edge
            cy.edges().removeClass('highlighted');
            edge.addClass('highlighted');

            // Dispatch event for Blazor
            this.dispatchEdgeSelected(edge.data());
        });

        // Background tap - deselect
        cy.on('tap', (event) => {
            if (event.target === cy) {
                this.selectedNode = null;
                this.selectedEdge = null;
                cy.edges().removeClass('highlighted');
            }
        });

        // Node mouseover
        cy.on('mouseover', 'node', (event) => {
            const node = event.target;
            node.connectedEdges().addClass('highlighted');
            document.body.style.cursor = 'pointer';
        });

        // Node mouseout
        cy.on('mouseout', 'node', () => {
            if (!this.selectedNode) {
                cy.edges().removeClass('highlighted');
            }
            document.body.style.cursor = 'default';
        });

        // Edge mouseover
        cy.on('mouseover', 'edge', () => {
            document.body.style.cursor = 'pointer';
        });

        // Edge mouseout
        cy.on('mouseout', 'edge', () => {
            document.body.style.cursor = 'default';
        });
    },

    /**
     * Dispatch node selected event to Blazor
     */
    dispatchNodeSelected: function (nodeData) {
        // We can call Blazor methods here if needed
        console.log('Node selected:', nodeData);
    },

    /**
     * Dispatch edge selected event to Blazor
     */
    dispatchEdgeSelected: function (edgeData) {
        console.log('Edge selected:', edgeData);
    },

    /**
     * Fit the graph to the viewport
     */
    fit: function () {
        if (this.cy) {
            this.cy.fit(null, 50);
        }
    },

    /**
     * Reset the layout
     */
    resetLayout: function () {
        if (this.cy) {
            this.cy.layout({
                name: 'cose',
                animate: true,
                animationDuration: 1000,
                nodeRepulsion: function (node) { return 8000; },
                idealEdgeLength: function (edge) { return 150; },
                edgeElasticity: function (edge) { return 100; },
                gravity: 0.25
            }).run();
        }
    },

    /**
     * Export graph as PNG
     */
    exportPng: function () {
        if (this.cy) {
            return this.cy.png({
                output: 'base64',
                bg: '#0f0f1a',
                full: true,
                scale: 2
            });
        }
        return null;
    },

    /**
     * Destroy the graph instance
     */
    destroy: function () {
        if (this.cy) {
            this.cy.destroy();
            this.cy = null;
        }
    }
};
